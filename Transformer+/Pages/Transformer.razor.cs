﻿using HtmlAgilityPack;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Radzen;
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
        string? _split, _join, _boundAll, _boundEach;
        private string input = string.Empty;
        private string output = string.Empty;
        private string userCode = string.Empty;
        private bool working = false, _dynamic;
        private readonly string error = "Input box is empty.";
        private List<Message> messages = [];

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

        private void Int() =>
            output = string.IsNullOrEmpty(input)
                ? error
                : input.Replace("\n", ", ");

        private void StrDouble() =>
            output = string.IsNullOrEmpty(input)
                ? error
                : string.Concat("\"", input.Replace("\n", "\", \""), "\"");

        private void StrSingle() =>
            output = string.IsNullOrEmpty(input)
                ? error
                : string.Concat("'", input.Replace("\n", "', '"), "'");

        private void Snippet() =>
            output = string.IsNullOrEmpty(input)
                ? error
                : string.Concat("\"", input.Replace("\n", "\",\n\"").Replace("\t", "\\t").Replace("    ", "\\t"), "\"");

        private void MixDouble()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            try
            {
                var inArray = input.Split("\n");

                if (inArray.Length <= 1)
                    inArray = input.Split("\t");

                var result = new StringBuilder();

                foreach (var item in inArray)
                {
                    result.AppendFormat(item switch
                    {
                        var a when int.TryParse(a, out var ignore) => "{0},",
                        _ => "\"{0}\","
                    }, item);
                }
                result.Length--;
                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void MixSingle()
        {
            if (string.IsNullOrEmpty(input))
            {
                output = error;
                return;
            }
            try
            {
                var inArray = input.Split("\n");

                if (inArray.Length <= 1)
                    inArray = input.Split("\t");

                var result = new StringBuilder();

                foreach (var item in inArray)
                {
                    result.AppendFormat(item switch
                    {
                        var a when int.TryParse(a, out var ignore) || a == "NULL" => "{0},",
                        _ => "'{0}',"
                    }, item);
                }
                result.Length--;
                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

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
                    }, Char.ToLowerInvariant(cols[x][0]) + cols[x][1..], values[x]);
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
                            properties[0] = properties[0].Replace("LOB", "LineOfBusiness").Replace("ID", "Id").Replace("Num", "Number").Replace("Agt", "Agent").Replace("Trans", "Transaction");
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

        private void Box()
        {
            try
            {
                var items = input.Split('\t');

                if (items.Length <= 1)
                    items = input.Split('\n');

                var result = new StringBuilder();
                foreach (var item in items)
                {
                    result.Append($"[{item}],\n\t");
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
                var userBox = "const input = document.getElementById('input').value;\nlet output = '';\n[***]\ndocument.getElementById('output').value = output;";
                if (!string.IsNullOrEmpty(userCode))
                {
                    HtmlDocument htmlDoc = new();
                    htmlDoc.LoadHtml(userCode);
                    var superClean = htmlDoc.DocumentNode.InnerText
                        .Replace("&gt;", ">").Replace("&lt;", "<").Replace("&nbsp;", " ");
                    Console.WriteLine(superClean);
                    await JsRuntime.InvokeAsync<string>("runUserScript", userBox.Replace("[***]", superClean));
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }

        private void StarterCode() =>             
            userCode = @"output = input.split('\n').map(x => x.length >= 5 ? `Hello ${x}` : `GoodBye ${x}`).join(',\n');";
    }
}
