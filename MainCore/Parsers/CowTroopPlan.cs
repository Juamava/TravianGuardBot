using System.Globalization;

namespace MainCore.Parsers
{
    public readonly record struct CowTroopSlot(char Letter, int TroopIndex, int Amount);

    public static class CowTroopPlan
    {
        // Rally point grid, same letters for every tribe:
        // A t1   D t4   G t7
        // B t2   E t5   H t8
        // C t3   F t6
        private static readonly (char Letter, int TroopIndex)[] Slots =
        [
            ('A', 1), ('D', 4), ('G', 7),
            ('B', 2), ('E', 5), ('H', 8),
            ('C', 3), ('F', 6),
        ];

        public static string Format(IEnumerable<CowTroopSlot> slots)
        {
            return string.Join(" ", slots.Select(slot => $"{slot.Letter}:{slot.Amount}"));
        }

        public static string Describe(string? stored, int legacyCount)
        {
            var slots = Resolve(stored, legacyCount);
            return slots.Count == 0 ? "" : Format(slots);
        }

        public static IReadOnlyList<CowTroopSlot> Resolve(string? stored, int legacyCount)
        {
            if (!string.IsNullOrWhiteSpace(stored) && TryParse(stored, out var slots, out _)) return slots;
            if (legacyCount > 0) return [new CowTroopSlot('A', 1, legacyCount)];
            return [];
        }

        public static bool TryParse(string? input, out List<CowTroopSlot> slots, out string error)
        {
            slots = [];
            error = "";
            var text = (input ?? "").Trim();
            if (text.Length == 0)
            {
                error = "Indicá las tropas. Un número solo usa A. Ejemplo: 2 o A:2 D:1.";
                return false;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var onlyAmount))
            {
                if (onlyAmount <= 0)
                {
                    error = "La cantidad tiene que ser un entero mayor que 0.";
                    return false;
                }

                slots.Add(new CowTroopSlot('A', 1, onlyAmount));
                return true;
            }

            var tokens = text.ToUpperInvariant()
                .Replace(',', ' ')
                .Replace(';', ' ')
                .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            var amounts = new Dictionary<char, int>();
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Replace('=', ':');
                if (token.Length == 1 && token[0] is >= 'A' and <= 'H')
                {
                    if (i + 1 >= tokens.Length)
                    {
                        error = $"A {token} le falta la cantidad.";
                        return false;
                    }

                    token = token + ":" + tokens[i + 1];
                    i++;
                }

                if (!TryToken(token, out var letter, out var amount, out error)) return false;
                if (!amounts.TryAdd(letter, amount))
                {
                    error = $"La tropa {letter} está repetida.";
                    return false;
                }
            }

            foreach (var (letter, troopIndex) in Slots)
            {
                if (amounts.TryGetValue(letter, out var amount))
                    slots.Add(new CowTroopSlot(letter, troopIndex, amount));
            }

            return true;
        }

        public static bool TryBlocked(IReadOnlyDictionary<int, int> known, IReadOnlyList<CowTroopSlot> slots, out string reason)
        {
            var parts = new List<string>();
            foreach (var slot in slots)
            {
                if (known.TryGetValue(slot.TroopIndex, out var available) && available < slot.Amount)
                    parts.Add($"{slot.Letter} has {available}, need {slot.Amount}");
            }

            reason = string.Join(", ", parts);
            return parts.Count > 0;
        }

        private static bool TryToken(string token, out char letter, out int amount, out string error)
        {
            letter = '\0';
            amount = 0;
            error = "";

            string letterPart;
            string amountPart;
            var colon = token.IndexOf(':');
            if (colon >= 0)
            {
                letterPart = token[..colon];
                amountPart = token[(colon + 1)..];
            }
            else if (token.Length >= 2 && char.IsLetter(token[0]) && token[1..].All(char.IsDigit))
            {
                letterPart = token[..1];
                amountPart = token[1..];
            }
            else if (token.Length >= 2 && char.IsLetter(token[^1]) && token[..^1].All(char.IsDigit))
            {
                letterPart = token[^1..];
                amountPart = token[..^1];
            }
            else
            {
                error = $"No entiendo \"{token}\". Usá A:2, A 2 o un número solo para A.";
                return false;
            }

            if (letterPart.Length != 1 || letterPart[0] is < 'A' or > 'H')
            {
                error = $"La tropa {letterPart} no existe. Las letras son A B C D E F G H.";
                return false;
            }

            if (!int.TryParse(amountPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                error = $"La cantidad de {letterPart} tiene que ser un entero mayor que 0.";
                return false;
            }

            letter = letterPart[0];
            return true;
        }
    }
}
