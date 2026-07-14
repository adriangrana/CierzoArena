using System.Globalization;
using System.Text;

namespace CierzoArena.Online.Identity
{
    public static class PlayerDisplayName
    {
        public const int MinLength = 3;
        public const int MaxLength = 20;

        public static string FallbackFor(string playerId)
        {
            string suffix = string.IsNullOrWhiteSpace(playerId) ? "0000" : playerId.Replace("-", string.Empty);
            return "Jugador-" + suffix.Substring(0, System.Math.Min(4, suffix.Length)).PadLeft(4, '0');
        }

        public static bool TryNormalize(string value, out string normalized, out string error)
        {
            normalized = string.Empty; error = string.Empty;
            if (value == null) { error = "El nombre es obligatorio."; return false; }
            StringBuilder builder = new StringBuilder();
            bool previousSpace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsControl(character)) { error = "El nombre contiene caracteres no válidos."; return false; }
                if (char.IsWhiteSpace(character))
                {
                    if (!previousSpace) builder.Append(' ');
                    previousSpace = true;
                }
                else { builder.Append(character); previousSpace = false; }
            }
            normalized = builder.ToString().Normalize(NormalizationForm.FormC);
            int visible = new StringInfo(normalized).LengthInTextElements;
            if (visible < MinLength || visible > MaxLength) { error = "El nombre debe tener entre 3 y 20 caracteres."; return false; }
            return true;
        }
    }
}
