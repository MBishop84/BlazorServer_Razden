using System.Text;

namespace Transformer_.Pages
{
    public partial class Transformer
    {
        private string input { get; set; } = string.Empty;
        private string output { get; set; } = string.Empty;
        private string error { get; set; } = "Input box is empty.";

        private void Clear()
        {
            input = string.Empty;
            output = string.Empty;
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
                var result = new StringBuilder();

                foreach (var item in inArray)
                {
                    result.AppendFormat(item switch
                    {
                        var a when int.TryParse(a, out var ignore) => "{0},",
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
                    }, cols[x], values[x]);
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

                if(items.Length <= 1)
                    items = input.Split('\n');

                var result = new StringBuilder();
                foreach (var item in items)
                {
                    result.Append($"[{item}], ");
                }
                output = result.ToString();
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
                var items = input.Split('\t');
                var result = new StringBuilder();
                foreach (var item in items)
                {
                    // result.Append($"{item} = p.{item},\n");
                    result.Append($"<td>@row.{item}</td>\n");
                }
                output = result.ToString();
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }
        }
    }
}
