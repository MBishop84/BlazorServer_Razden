using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Radzen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Formatting = Newtonsoft.Json.Formatting;

namespace Transformer_.Pages
{
    public partial class Transformer
    {
        #region Injected Services

        [Inject]
        private IJSRuntime JS { get; init; }
        [Inject]
        private DialogService DialogService { get; init; }
        [Inject]
        private IConfiguration Configuration { get; init; }

        #endregion

        #region Feilds
        private record JsTransform(int Id, string AddedBy, string Name, string Code);
        private StandaloneCodeEditor _editor { get; set; }
        private List<string> MonacoThemes = [];
        private List<JsTransform> JsTransforms = [];
        private int _height = 1000;
        private bool _dynamic, _sort;
        public string? Input, Output, Split, Join, BoundAll, BoundEach, MonacoTheme, JsFilePath;
        private string? _entry;

        #endregion

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            try
            {
                MonacoThemes = Directory.GetFiles("wwwroot/themes/", "*.json")
                    .Select(x => Path.GetFileNameWithoutExtension(x)).ToList();
                MonacoThemes.AddRange(new[] { "vs-dark", "vs-light" });
                JsFilePath = Configuration.GetValue<string>("JsTransformsFile")
                             ?? throw new ArgumentException("JsTransformsFile config missing");
                var json = await File.ReadAllTextAsync(JsFilePath);
                if (string.IsNullOrEmpty(json))
                {
                    await using StreamWriter outputFile = new(JsFilePath, false);
                    await outputFile.WriteLineAsync(JsonConvert.SerializeObject(new List<JsTransform>
                    {
                        new(0,"Unknown", "//Converter", @"output = input.split('\t').map(x => `${x} = source.${x}`).join(',\n')")
                    }));
                }
                JsTransforms = JsonConvert.DeserializeObject<JsTransform[]>(json)?.ToList()!;
                DialogService.OnClose += DialogClose;
            }
            catch (Exception ex)
            {
                Input = ex.Message;
                Output = ex.Message;
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            _height = await JS.InvokeAsync<int>("GetHeight");
            var theme = await JS.InvokeAsync<string>("localStorage.getItem", "MonacoTheme");
            if (string.IsNullOrEmpty(theme))
            {
                await ChangeTheme("vs-dark");
            }
            else
            {
                await ChangeTheme(theme);
            }

            await InvokeAsync(StateHasChanged);
        }

        private void DialogClose(dynamic entry)
        {
            if (entry == null)
                return;

            _entry = $"{entry}";
            StateHasChanged();
        }

        private async Task ChangeTheme(string theme)
        {
            MonacoTheme = theme;
            try
            {
                var myTheme = theme;
                if (!new[] { "vs-dark", "vs-light" }.Contains(theme))
                {
                    await Global.DefineTheme(JS, "thisTheme",
                        JsonConvert.DeserializeObject<StandaloneThemeData>(
                            await File.ReadAllTextAsync($"wwwroot/themes/{theme}.json")));
                    myTheme = "thisTheme";
                }

                await Global.SetTheme(JS, myTheme);
                await JS.InvokeVoidAsync("localStorage.setItem", "MonacoTheme", theme);
            }
            catch (Exception ex)
            {
                await DialogService.Alert(ex.StackTrace, ex.Message);
            }
        }

        private async Task Transform()
        {
            try
            {
                if (!string.IsNullOrEmpty(BoundAll) && BoundAll.Split('.').Length != 2)
                {
                    throw new ArgumentException("Bound-All must be separated by a period(.)");
                }
                if (!string.IsNullOrEmpty(BoundEach) && BoundEach.Split('.').Length != 2)
                {
                    throw new ArgumentException("Bound-Each must be separated by a period(.)");
                }
                var split = Split?.Replace("\\n", "\n").Replace("\\t", "\t") ?? string.Empty;
                var join = Join?.Replace("\\n", "\n").Replace("\\t", "\t") ?? string.Empty;

                var frontBracket = string.IsNullOrEmpty(BoundAll)
                    ? string.Empty
                    : BoundAll.Split('.')[0];
                var endBracket = string.IsNullOrEmpty(BoundAll)
                    ? string.Empty
                    : BoundAll.Split('.')[1];
                var frontParentheses = string.IsNullOrEmpty(BoundEach)
                    ? string.Empty
                    : BoundEach.Split('.')[0];
                var endParentheses = string.IsNullOrEmpty(BoundEach)
                    ? string.Empty
                    : BoundEach.Split('.')[1];

                var outputArray = Input?.Split(split).Select(x =>
                {
                    return _dynamic switch
                    {
                        true => int.TryParse(x, out var i) || x.Equals("null", StringComparison.OrdinalIgnoreCase)
                            ? $"{x}"
                            : $"{frontParentheses}{x}{endParentheses}",
                        false => $"{frontParentheses}{x}{endParentheses}"
                    };
                }) ?? [];

                if (_sort)
                {
                    outputArray = outputArray.OrderBy(x => x).ToImmutableList();
                }

                Output = $"{frontBracket}{string.Join(join, outputArray)}{endBracket}";
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "Transform Error",
                        new Dictionary<string, object>
                        {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private void ClearField(string field) =>
            GetType().GetField(field)?.SetValue(this, default);

        private async Task RowToJson()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                {
                    throw new ArgumentException("Input is empty.");
                }
                var cols = Input.Split("\n")[0].Split("\t");
                var values = Input.Split("\n")[1].Split("\t");
                var result = new StringBuilder();

                for (var x = 0; x < cols.Length; x++)
                {
                    result.AppendFormat(values[x] switch
                    {
                        var a when int.TryParse(a, out int ignored) => "\"{0}\":{1},\n",
                        var b when b.Equals("NULL", StringComparison.OrdinalIgnoreCase) => "\"{0}\":\"\",\n",
                        var c when string.IsNullOrEmpty(c) => "\"{0}\":\"\",\n",
                        _ => "\"{0}\":\"{1}\",\n"
                    }, char.ToLowerInvariant(cols[x][0]) + cols[x][1..], values[x]);
                }
                result.Length -= 2;
                Output = $"{{\n{result}\n}}";
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "RowToJson Error",
                    new Dictionary<string, object>
                    {
                        { "Type", Enums.DialogTypes.Error },
                        { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task ClassFromQuery()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                {
                    throw new ArgumentException("Input is Empty");
                }
                var lines = Input.Split("\n");
                var result = new StringBuilder();

                foreach (var line in lines)
                {
                    var properties = line.Split("\t");
                    result.Append($"///<summary>\n/// Gets/Sets the {properties[0]}.\n///</summary>\n");
                    switch (properties.Length)
                    {
                        case 1:
                            throw new ArgumentException("Insufficient arguments.\n\n");
                        case 2:
                            result.Append(properties[1] switch
                            {
                                var a when a.Contains("int", StringComparison.OrdinalIgnoreCase) =>
                                    $"public int {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("date", StringComparison.OrdinalIgnoreCase) =>
                                    $"public DateTime {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("bit", StringComparison.OrdinalIgnoreCase) =>
                                    $"public bool {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("unique", StringComparison.OrdinalIgnoreCase) =>
                                    $"public Guid {properties[0]} {{ get; set; }}\n\n",
                                _ => $"public string {properties[0]} {{ get; set; }}\n\n"
                            });
                            break;
                        case 3:
                            var isNullable = properties[2].Equals("YES", StringComparison.OrdinalIgnoreCase) ? "?" : "";
                            result.Append(properties[1] switch
                            {
                                var a when a.Contains("int", StringComparison.OrdinalIgnoreCase) =>
                                    $"public int{isNullable} {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("date", StringComparison.OrdinalIgnoreCase) =>
                                    $"public DateTime{isNullable} {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("bit", StringComparison.OrdinalIgnoreCase) =>
                                    $"public bool{isNullable} {properties[0]} {{ get; set; }}\n\n",
                                var b when b.Contains("unique", StringComparison.OrdinalIgnoreCase) =>
                                    $"public Guid{isNullable} {properties[0]} {{ get; set; }}\n\n",
                                _ => $"public string {properties[0]} {{ get; set; }}\n\n"
                            });
                            break;
                    }
                }
                Output = result.ToString();
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "ClassFromQuery Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.ClassFromQuery },
                { "Message", $"{ex.Message}\n\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task JsonToClass()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                    throw new ArgumentException("Input is Empty");
                if (!Input.StartsWith("{"))
                    Input = $"{{{Input}}}";
                dynamic? jsonObject = JsonConvert.DeserializeObject(Input);
                if (jsonObject == null) return;
                var result = new StringBuilder();
                List<string> nestedItems = [];
                foreach (var i in jsonObject)
                {
                    var nested = false;
                    foreach (var x in i)
                    {
                        if (!x.HasValues) continue;
                        var objectName = $"{i.Name}";
                        result.AppendFormat(
                            "///<summary>\n/// Gets/Sets the {0}.\n///</summary>\npublic {1} {0} {{ get; set; }}",
                            i.Name,
                            $"{char.ToUpper(objectName[0])}{objectName[1..]}");
                        nestedItems.Add($"{char.ToUpper(objectName[0])}{objectName[1..]}");
                        nested = true;
                    }

                    if (nested)
                    {
                        result.Append("}\n");
                        continue;
                    }
                    result.AppendFormat("///<summary>\n/// Gets/Sets the {0}.\n///</summary>\n", i.Name);
                    result.Append($"{i.Value}" switch
                    {
                        var a when int.TryParse(a, out var itemInt) =>
                            $"public int {i.Name} {{ get; set; }}\n\n",
                        var b when DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out var itemDate) =>
                            $"public DateTime? {i.Name} {{ get; set; }}\n\n",
                        var c when bool.TryParse(c, out var iBool) =>
                            $"public bool {i.Name} {{ get; set; }}\n\n",
                        _ => $"public string {i.Name} {{ get; set; }} = string.Empty;\n\n"
                    });
                }
                Output = result.ToString();
                if (nestedItems.Any())
                    throw new ArgumentException(
                        $"Nested objects are not fully supported.\n\n{string.Join("\n\t", nestedItems)}\n\nHave been added as objects.");
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "JsonToClass Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task JsonToXML()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                    throw new ArgumentException("Input is Empty");
                if (!Input.StartsWith("{"))
                    Input = $"{{{Input}}}";

                var doc = JsonConvert.DeserializeXmlNode(
                    "{\"DefaultRoot\":" + $"{Input}}}"
                );
                var sw = new StringWriter();
                var writer = new XmlTextWriter(sw)
                {
                    Formatting = System.Xml.Formatting.Indented
                };
                doc?.WriteContentTo(writer);
                Output = sw.ToString();
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "JsonToXML Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task XmlToClass()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                    throw new ArgumentException("Input is Empty");

                if (Input.Contains("&lt;") || Input.Contains("&gt;"))
                    Input = Input.Replace("&lt;", "<").Replace("&gt;", ">");

                var xml = new XmlDocument();
                xml.LoadXml(Input);
                var result = new StringBuilder();
                var xmlRoot = xml.DocumentElement;

                if (xmlRoot == null)
                    throw new ArgumentException("XML must have a root element");

                result.Append(
                    $"public class {xmlRoot.Name}\n{{\n");

                foreach (XmlNode node in xmlRoot.ChildNodes)
                {
                    if (node.NodeType == XmlNodeType.Comment) continue;
                    if (string.IsNullOrEmpty(node.InnerText) || node.ChildNodes.Count > 1)
                    {
                        result.AppendFormat(
                            "\t///<summary>\n\t/// Gets/Sets the {0}.\n\t///</summary>\n\tpublic {1} {0} {{ get; set; }}\n\n",
                            node.Name,
                            $"{char.ToUpper(node.Name[0])}{node.Name[1..]}");
                    }
                    else
                    {
                        result.AppendFormat(
                            "\t///<summary>\n\t/// Gets/Sets the {0}.\n\t///</summary>\n\tpublic {1} {0} {{ get; set; }} = {2};\n\n",
                            node.Name,
                            node.InnerText switch
                            {
                                var a when int.TryParse(a, out var itemInt) => "int",
                                var b when DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out var itemDate) => "DateTime",
                                var c when bool.TryParse(c, out var iBool) => "bool",
                                _ => "string"
                            },
                            node.InnerText switch
                            {
                                var a when int.TryParse(a, out var itemInt) => "0",
                                var b when DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out var itemDate) => "DateTime.MinValue",
                                var c when bool.TryParse(c, out var iBool) => "false",
                                _ => "string.Empty"
                            });
                    }
                }
                result.Append("}");
                Output = result.ToString();
            }
            catch (Exception ex)
            {
                var message = $"{ex.Message}\n{ex.StackTrace}";
                if (ex.Message.Contains("multiple root elements"))
                    message = $"{ex.Message}\n\nPlease add an outer root element.";
                await DialogService.OpenAsync<CustomDialog>(
                    "XmlToClass Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.XmlToClass },
                { "Message", message }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task XmlToJson()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                    throw new ArgumentException("Input is Empty");
                var doc = new XmlDocument();
                doc.LoadXml(Input);
                Output = JsonConvert.SerializeXmlNode(doc, Formatting.Indented);
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "XmlToJson Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task JavaScript()
        {
            try
            {
                if (string.IsNullOrEmpty(Input))
                    throw new ArgumentException("Input is Empty");

                var userCode = await _editor.GetValue();

                if (string.IsNullOrEmpty(userCode))
                    throw new ArgumentException("Please enter or choose a function.");
                if (!userCode.Contains("output ="))
                    throw new ArgumentException("Please assign a value to output");
                if (!userCode.Contains("= input"))
                    throw new ArgumentException("You must use the input.");

                const string userBox = "const input = document.getElementById('input').value;\nlet output = '';\n[***]\ndocument.getElementById('output').value = output;";
                var task = JS.InvokeAsync<string>("runUserScript", userBox.Replace("[***]", userCode)).AsTask();
                if (await Task.WhenAny(task, Task.Delay(3)) != task)
                {
                    throw new ArgumentException("JavaScript Timeout. Please simplify your code.");
                }
                await task;
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "JavaScript Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task SaveJs()
        {
            try
            {
                var userCode = await _editor.GetValue();
                if (string.IsNullOrEmpty(userCode))
                    throw new ArgumentException("Input is Empty");
                var name = userCode.Split("\n")[0];
                if (!await DialogService.Confirm(
                    $"Is {name} the name for your transform?",
                    "Confirmation",
                    new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }) ?? false)
                {
                    await DialogService.OpenAsync<CustomDialog>(
                        "Enter Transform Name",
                        new Dictionary<string, object>
                        {
                     { "Type", Enums.DialogTypes.Text },
                     { "Message", "Please name your transform." }
                        },
                        new DialogOptions() { Width = "max-content", Height = "200px" });
                    if (string.IsNullOrEmpty(_entry))
                        throw new ArgumentException("Transform Name is Empty");
                    name = _entry.StartsWith("//") ? _entry : $"//{_entry}";
                    _entry = null;
                }
                else { userCode = userCode.Replace($"{name}\n", ""); }

                await DialogService.OpenAsync<CustomDialog>(
                    "Enter Name",
                    new Dictionary<string, object>
                    {
                 { "Type", Enums.DialogTypes.Text },
                 { "Message", "Please enter your name to take ownership of this transform." }
                    },
                    new DialogOptions() { Width = "max-content", Height = "200px" });

                if (string.IsNullOrEmpty(_entry))
                    throw new ArgumentException("Name is Empty");

                var newTransform = new JsTransform(
                    JsTransforms.Count,
                    _entry,
                    name,
                    userCode);

                JsTransforms.Add(newTransform);
                await File.WriteAllTextAsync(JsFilePath, JsonConvert.SerializeObject(JsTransforms, Formatting.Indented));
                await InvokeAsync(StateHasChanged);
                await DialogService.Alert(
                    JsonConvert.SerializeObject(newTransform, Formatting.Indented),
                    "Success");
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                "SaveJs Error",
                new Dictionary<string, object>
                {
             { "Type", Enums.DialogTypes.Error },
             { "Message", $"{ex.Message}\n\n{ex.StackTrace}" }
                },
                new DialogOptions()
                {
                    Width = "max-content",
                    Height = "50vh"
                });
            }
        }

        private async Task DeleteJs()
        {
            try
            {
                var userCode = await _editor.GetValue();
                var name = userCode.Split("\n")[0];
                var jsTransform = JsTransforms.FirstOrDefault(x => x.Name == name);
                var deleteMessage = $"Your key does not match, but {name} has been deleted from this instance.";
                if (jsTransform == null)
                    throw new ArgumentException("No code found to delete.");

                if (await DialogService.Confirm(
                    $"Are you sure you want to delete {name}?",
                    "Final Confirmation",
                    new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }) ?? false)
                {
                    await DialogService.OpenAsync<CustomDialog>("Password", new Dictionary<string, object>
                    {
                        { "Type", Enums.DialogTypes.Password },
                        { "Message", "Please enter your key to permanently delete this code." }
                    }, new DialogOptions() { Width = "max-content", Height = "200px" });

                    if (string.IsNullOrEmpty(_entry))
                        throw new ArgumentException("Password is Empty");

                    using var hash = SHA256.Create();
                    if (Configuration.GetValue<string>("DeleteKey")
                        !.Equals(Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(_entry)))))
                    {
                        JsTransforms.Remove(jsTransform);
                        await File.WriteAllTextAsync(JsFilePath, JsonConvert.SerializeObject(JsTransforms, Formatting.Indented));
                        deleteMessage = $"{name} has been permanently deleted.";
                    }
                    else
                    {
                        JsTransforms.Remove(jsTransform);
                    }
                    await _editor.SetValue(string.Empty);
                    await InvokeAsync(StateHasChanged);
                    await DialogService.Alert(deleteMessage, "Success!");
                }
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "DeleteJs Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private Task UpdateUserCode(string jsTransform)
        {
            var fullString = JsTransforms.Where(x => x.Code == jsTransform)
                .Select(y => $"{y.Name}\n{y.Code}").FirstOrDefault();
            return _editor.SetValue(fullString);
        }

        private async Task NextJs()
        {
            try
            {
                var userCode = await _editor.GetValue();
                if (string.IsNullOrEmpty(userCode))
                    userCode = $"{JsTransforms[0].Name}\n{JsTransforms[0].Code}";
                else
                {
                    var index = JsTransforms.FindIndex(x => x.Name == userCode.Split("\n")[0]);
                    userCode = index < JsTransforms.Count - 1
                        ? $"{JsTransforms[index + 1].Name}\n{JsTransforms[index + 1].Code}"
                        : $"{JsTransforms[0].Name}\n{JsTransforms[0].Code}";
                }
                await _editor.SetValue(userCode);
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "NextJs Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private async Task PreviousJs()
        {
            try
            {
                var userCode = await _editor.GetValue();
                if (string.IsNullOrEmpty(userCode))
                    userCode = $"{JsTransforms[0].Name}\n{JsTransforms[0].Code}";
                else
                {
                    var index = JsTransforms.FindIndex(x => x.Name == userCode.Split("\n")[0]);
                    if (index > 0)
                    {
                        userCode = $"{JsTransforms[index - 1].Name}\n{JsTransforms[index - 1].Code}";
                    }
                    else
                        userCode = $"{JsTransforms[^1].Name}\n{JsTransforms[^1].Code}";
                }
                await _editor.SetValue(userCode);
            }
            catch (Exception ex)
            {
                await DialogService.OpenAsync<CustomDialog>(
                    "PreviousJs Error",
                    new Dictionary<string, object>
                    {
                { "Type", Enums.DialogTypes.Error },
                { "Message", $"{ex.Message}\n\n{ex.StackTrace}" }
                    },
                    new DialogOptions()
                    {
                        Width = "max-content",
                        Height = "50vh"
                    });
            }
        }

        private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
        {
            return new StandaloneEditorConstructionOptions
            {
                AutomaticLayout = true,
                Language = "javascript",
                Value = "//StarterCode\noutput = input;",
                TabSize = 2,
                DetectIndentation = true,
                TrimAutoWhitespace = true,
                WordBasedSuggestionsOnlySameLanguage = true,
                StablePeek = true
            };
        }
    }
}
