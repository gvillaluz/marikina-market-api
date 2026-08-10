using System.Text;
using System.Text.Json;

namespace MarikinaMarket.API.Infrastructure.Json
{
    public class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var sb = new StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];

                if (char.IsUpper(c))
                {
                    if (i > 0)
                        sb.Append('_');

                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}