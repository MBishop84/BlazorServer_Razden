using BlazorMonaco.Editor;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Radzen;
using System.Collections;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;

namespace Transformer_.Pages
{
    public partial class Transformer
    {
        [Inject]
        private IConfiguration Configuration { get; set; }
        [Inject]
        private TooltipService tooltipService { get; set; }
        [Inject]
        private IJSRuntime JsRuntime { get; set; }
        [Inject]
        public DialogService DialogService { get; set; }
        StandaloneCodeEditor _editor { get; set; }
        private sealed record UserJs(string Name, string Code);
        #region Fields

        public string? Split, Join, BoundAll, BoundEach;

        #endregion Fields
        List<UserJs> jsTransforms = [];
        private string? jsTransform;
        private string input = string.Empty;
        private string output = string.Empty;
        private string userCode = string.Empty;
        private bool working = false, _dynamic;
        private readonly string error = "Input box is empty.";
        private List<Message> messages = [];

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var filePath = Configuration.GetValue<string>("JsTransformsFile")
                               ?? throw new ArgumentException("JsTransformsFile config missing");
                var json = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrEmpty(json))
                {
                    await using StreamWriter outputFile = new(filePath, false);
                    await outputFile.WriteLineAsync(JsonConvert.SerializeObject(new List<UserJs>
                    {
                        new("//Converter", "output = input.split('\\t').map(x => `${x} = source.${x}`).join(',\\n')")
                    }));
                }
                jsTransforms = JsonConvert.DeserializeObject<UserJs[]>(json)?.ToList()!;
            }
            catch (Exception ex)
            {
                await DialogService.Alert(ex.StackTrace,ex.Message);
            }
        }

        private void ShowTooltip(ElementReference elementReference, TooltipOptions options)
        {
            options.Duration = 5000;
            tooltipService.Open(elementReference, options.Text, options);
        }

        private sealed class Message
        {
            public string? role { get; set; }
            public string? content { get; set; }
        }

        private void Clear()
        {
            input = string.Empty;
            output = string.Empty;
        }

        private async Task AskGpt()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            else if (string.IsNullOrEmpty(Configuration["GPTKey"]))
            {
                output = "You are missing the api key.";
                return;
            }
            output = "Please wait...";
            working = true;
            try
            {
                messages.Add(new Message() { role = "user", content = input });
                output = await CallOpenAi() ?? error;
                messages.Add(new Message() { role = "assistant", content = output });
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
            finally
            {
                working = false;
            }
        }

        private async Task<string?> CallOpenAi()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Configuration["GPTKey"]);
                var request = new
                {
                    model = "gpt-3.5-turbo",
                    messages = messages.ToArray(),
                };
                var response = await client.PostAsync(
                    @"https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonConvert.SerializeObject(request),
                    Encoding.UTF8, "application/json"));

                var result = await response.Content.ReadAsStringAsync();
                dynamic gptResponse = JsonConvert.DeserializeObject(result);
                return gptResponse?.choices[0].message.content;
            }
            catch (Exception ex)
            {
                return ex.Message;
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

                output = $"{frontBracket}{string.Join(join, input?.Split(split).Select(x =>
                {
                    return _dynamic switch
                    {
                        true => int.TryParse(x, out var i) || x.Equals("null", StringComparison.OrdinalIgnoreCase)
                            ? $"{x}" : $"{frontParentheses}{x}{endParentheses}",
                        false => $"{frontParentheses}{x}{endParentheses}"
                    };
                }) ?? [])}{endBracket}";
            }
            catch (Exception ex)
            {
                await DialogService.Alert(ex.StackTrace, ex.Message);
            }
        }

        private void ClearField(string field)
        {
            try
            {
                GetType().GetField(field)?.SetValue(this, default);
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void Snippet() =>
            output = string.IsNullOrEmpty(input)
                ? error
                : string.Concat("\"", input.Replace("\n", "\",\n\"").Replace("\t", "\\t").Replace("    ", "\\t"), "\"");

        private void Json()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            try
            {
                var cols = input.Split("\n")[0].Split("\t");
                var values = input.Split("\n")[1].Split("\t");
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
                output = $"{{\n{result}\n}}";
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void Entity()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            try
            {
                var lines = input.Split("\n");
                var result = new StringBuilder();

                foreach (var line in lines)
                {
                    var properties = line.Split("\t");
                    result.AppendFormat("///<summary>\n/// Gets/Sets the {0}.\n///</summary>\n", properties[0]);
                    switch (properties.Length)
                    {
                        case 1:
                            result.Append($"Insufficient arguments.\n\n");
                            break;
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
                            string isNullable = properties[2].Equals("YES", StringComparison.OrdinalIgnoreCase) ? "?" : "";
                            properties[0] = properties[0].Replace("LOB", "LineOfBusiness").Replace("ID", "Id").Replace("Num", "Number").Replace("Agt", "Agent").Replace("Trans", "Transaction");
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
                        default:
                            break;
                    }
                }
                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void Conversion()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            try
            {
                var lines = input.Split("\n");
                var result = new StringBuilder();

                foreach (var line in lines)
                {
                    var param = line.Replace("LOB", "LineOfBusiness").Replace("ID", "Id").Replace("Num", "Number").Replace("Agt", "Agent").Replace("Trans", "Transaction");
                    result.AppendLine($"{param} = source.{param},");
                }

                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.ToString();
            }
        }

        private void Schema()
        {
            try
            {
                var items = input.Split('\n');
                var result = new StringBuilder();
                foreach (var item in items)
                {
                    result.Append($"builder.Property(p => p.{item}).HasColumnName(\"{item}\");\n\n");
                }
                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void JsonToClass()
        {
            try
            {
                dynamic? jsonObject = JsonConvert.DeserializeObject($"{{{input}}}");
                if (jsonObject != null)
                {
                    var result = new StringBuilder();
                    foreach (var i in jsonObject)
                    {
                        result.AppendFormat("///<summary>\n/// Gets/Sets the {0}.\n///</summary>\n", i.Name);
                        result.Append($"{i.Value}" switch
                        {
                            var a when int.TryParse(a, out int itemInt) =>
                                $"public int {i.Name} {{ get; set; }}\n\n",
                            var b when DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime itemDate) =>
                                $"public DateTime? {i.Name} {{ get; set; }}\n\n",
                            var c when bool.TryParse(c, out bool itemBool) =>
                                $"public bool {i.Name} {{ get; set; }}\n\n",
                            _ => $"public string {i.Name} {{ get; set; }} = string.Empty;\n\n"
                        });
                    }
                    output = result.ToString();
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void JsonToXML()
        {
            try
            {
                var doc = JsonConvert.DeserializeXmlNode(
                    @"{'DefaultRoot':{" + $"{input}}}}}"
                    );
                var sw = new StringWriter();
                var writer = new XmlTextWriter(sw)
                {
                    Formatting = System.Xml.Formatting.Indented
                };
                doc?.WriteContentTo(writer);
                output = sw.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void Wild()
        {
            try
            {
                //output = string.Join("", input.Split('\n').Select(x =>
                //{
                //    var thisItem = x.Split(':');
                //    return thisItem[0].Contains('?')
                //        ? $"{x.Replace(";", "")} | null;\n"
                //        : $"{thisItem[0]}?: {thisItem[1].Replace(";", "")} | null;\n";
                //}));

                output = string.Join("", input.Split('\n').Select(x =>
                {
                    var y = x.Split('\t');
                    y[1] = y[1].Equals("varchar") ? "varchar(50)" : y[1];
                    return $"[{y[0]}] {y[1]},\n\t";
                }));
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private async Task JavaScript()
        {
            try
            {
                userCode = await _editor.GetValue();
                var userBox = "const input = document.getElementById('input').value;\nlet output = '';\n[***]\ndocument.getElementById('output').value = output;";
                if (!string.IsNullOrEmpty(userCode))
                {
                    await JsRuntime.InvokeAsync<string>("runUserScript", userBox.Replace("[***]", userCode));
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private async Task NextJs()
        {
            try
            {
                userCode = await _editor.GetValue();
                if (string.IsNullOrEmpty(userCode))
                    userCode = $"{jsTransforms[0].Name}\n{jsTransforms[0].Code}";
                else
                {
                    var index = jsTransforms.FindIndex(x => x.Name == userCode.Split("\n")[0]);
                    if (index < jsTransforms.Count - 1)
                    {
                        userCode = $"{jsTransforms[index + 1].Name}\n{jsTransforms[index + 1].Code}";
                    }
                    else
                        userCode = $"{jsTransforms[0].Name}\n{jsTransforms[0].Code}";
                }
                await _editor.SetValue(userCode);
            }
            catch (Exception ex)
            {
                await DialogService.Alert(ex.StackTrace,ex.Message);
            }
        }

        private async Task PreviousJs()
        {
            try
            {
                userCode = await _editor.GetValue();
                if (string.IsNullOrEmpty(userCode))
                    userCode = $"{jsTransforms[0].Name}\n{jsTransforms[0].Code}";
                else
                {
                    var index = jsTransforms.FindIndex(x => x.Name == userCode.Split("\n")[0]);
                    if (index > 0)
                    {
                        userCode = $"{jsTransforms[index - 1].Name}\n{jsTransforms[index - 1].Code}";
                    }
                    else
                        userCode = $"{jsTransforms[^1].Name}\n{jsTransforms[^1].Code}";
                }
                await _editor.SetValue(userCode);
            }
            catch (Exception ex)
            {
                await DialogService.Alert(ex.StackTrace,ex.Message);
            }
        }

        private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
        {
            return new StandaloneEditorConstructionOptions
            {
                AutomaticLayout = true,
                Language = "javascript",
                Value = userCode,
                Theme = "vs-dark",
                TabSize = 2,
                DetectIndentation = true,
                TrimAutoWhitespace = true,
                WordBasedSuggestionsOnlySameLanguage = true,
                StablePeek = true
            };
        }
    }
}
