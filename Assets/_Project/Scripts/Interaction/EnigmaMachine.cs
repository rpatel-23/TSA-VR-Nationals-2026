// -----------------------------------------------------------------------------
//  EnigmaMachine.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2, core)
//
//  A historically-INSPIRED, pedagogically-calibrated rotor cipher. It reproduces
//  the two properties that make Enigma intuitive to teach in VR:
//
//    1. RECIPROCITY — encryption and decryption are the SAME operation. With the
//       rotors at the right key, typing the ciphertext yields the plaintext and
//       vice-versa. (Real Enigma had this because of its reflector.)
//    2. STEPPING    — the mapping changes after every keypress, because the rotors
//       advance like an odometer. The same letter pressed twice usually lights a
//       different lamp. This is what made it dramatically stronger than Caesar.
//
//  It is NOT a bit-exact replica of any wartime Enigma wiring (no attempt is made
//  to match historical rotor wirings, ring settings or plugboard). Instead, the
//  per-step mapping is a self-inverse permutation (an "involution") generated so
//  that ONE chosen key deterministically produces the museum's scripted message,
//  while every other setting produces reversible gibberish that visibly changes
//  as the rotors turn. This guarantees the recorded demo always works while still
//  honestly demonstrating the real concepts. (See 04_* / 06_* docs.)
//
//  Calibration is fully data-driven: give it the solution key, the plaintext word
//  and the ciphertext word, and it derives the per-position letter swaps itself.
//  A self-mapping pair (letter == its partner) is impossible in an involution and
//  is reported as a configuration error, mirroring Enigma's real no-self-encrypt
//  property.
//
//  CONVENTION: encode-at-current-state, THEN advance. Rotor index 2 is the
//  fastest (rightmost) wheel. Key string "MAC" => rotor0='M', rotor1='A',
//  rotor2='C'; stepping increments rotor2 first, carrying left on wrap.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class EnigmaMachine : MonoBehaviour
    {
        [Header("Calibration (data-driven)")]
        [Tooltip("Rotor key that decodes the scripted message (3 letters).")]
        [SerializeField] private string _solutionKey = "MAC";
        [Tooltip("Plaintext word the exhibit teaches.")]
        [SerializeField] private string _plaintextWord = "TSANLC";
        [Tooltip("Ciphertext that decodes to the plaintext at the solution key.")]
        [SerializeField] private string _cipherWord = "XJQBVK";

        // Working rotor state (the live machine position).
        private int _r0, _r1, _r2;

        // Cache of involution tables keyed by packed configuration code.
        private readonly Dictionary<int, int[]> _involutionCache = new Dictionary<int, int[]>();
        // For the calibrated configs we remember the forced swap pair (a,b).
        private readonly Dictionary<int, (int a, int b)> _calibratedPairs = new Dictionary<int, (int, int)>();

        private bool _built;

        private void Awake() => Build();

        // ---------------------------------------------------------------- build

        /// <summary>(Re)derive the calibration. Safe to call after editing fields.</summary>
        public void Build()
        {
            _involutionCache.Clear();
            _calibratedPairs.Clear();

            string key = Sanitise(_solutionKey, 3, "MAC");
            string plain = Sanitise(_plaintextWord, _plaintextWord.Length, "TSANLC");
            string cipher = Sanitise(_cipherWord, _cipherWord.Length, "XJQBVK");

            if (plain.Length != cipher.Length)
            {
                Debug.LogError($"[EnigmaMachine] plaintext ('{plain}') and ciphertext " +
                               $"('{cipher}') must be equal length. Calibration skipped.");
                _built = true;
                return;
            }

            // Walk the rotor odometer exactly as a real run would, registering the
            // required swap at each visited configuration.
            int r0 = key[0] - 'A', r1 = key[1] - 'A', r2 = key[2] - 'A';
            for (int i = 0; i < plain.Length; i++)
            {
                int a = plain[i] - 'A';
                int b = cipher[i] - 'A';
                if (a == b)
                {
                    Debug.LogError($"[EnigmaMachine] position {i}: '{plain[i]}' maps to itself — " +
                                   "impossible for a reciprocal machine (no letter encodes to itself). " +
                                   "Choose a different word pair.");
                    continue;
                }
                int code = Pack(r0, r1, r2);
                if (_calibratedPairs.ContainsKey(code))
                    Debug.LogWarning($"[EnigmaMachine] configuration {code} reused during calibration; " +
                                     "message longer than the fast rotor period. Later swap wins.");
                _calibratedPairs[code] = (a, b);
                Step(ref r0, ref r1, ref r2);
            }

            _built = true;
        }

        // ----------------------------------------------------------- public API

        /// <summary>Set the live rotor position (e.g. from the three physical dials).</summary>
        public void SetRotors(int r0, int r1, int r2)
        {
            _r0 = Wrap(r0); _r1 = Wrap(r1); _r2 = Wrap(r2);
        }

        public int Rotor(int index) => index == 0 ? _r0 : index == 1 ? _r1 : _r2;

        /// <summary>
        /// Encode one letter at the current rotor position, then advance the rotors.
        /// Returns the lit lamp letter. Non-letters pass through unchanged and do
        /// not step the machine (matching how Enigma ignored spaces).
        /// </summary>
        public char EncodeAndAdvance(char input)
        {
            if (!_built) Build();
            char up = char.ToUpperInvariant(input);
            if (up < 'A' || up > 'Z') return up;

            int[] inv = InvolutionFor(_r0, _r1, _r2);
            char outChar = (char)('A' + inv[up - 'A']);
            Step(ref _r0, ref _r1, ref _r2);
            return outChar;
        }

        /// <summary>
        /// Pure transform of a whole string from a given start key WITHOUT touching
        /// the live state. Handy for editor previews, tests and the demo director.
        /// </summary>
        public string Transform(string message, int r0, int r1, int r2)
        {
            if (!_built) Build();
            var sb = new System.Text.StringBuilder(message.Length);
            int a = Wrap(r0), b = Wrap(r1), c = Wrap(r2);
            foreach (char raw in message)
            {
                char up = char.ToUpperInvariant(raw);
                if (up < 'A' || up > 'Z') { sb.Append(up); continue; }
                int[] inv = InvolutionFor(a, b, c);
                sb.Append((char)('A' + inv[up - 'A']));
                Step(ref a, ref b, ref c);
            }
            return sb.ToString();
        }

        public string SolutionKey => Sanitise(_solutionKey, 3, "MAC");
        public string PlaintextWord => Sanitise(_plaintextWord, _plaintextWord.Length, "TSANLC");
        public string CipherWord => Sanitise(_cipherWord, _cipherWord.Length, "XJQBVK");

        // ------------------------------------------------------ involution table

        private int[] InvolutionFor(int r0, int r1, int r2)
        {
            int code = Pack(r0, r1, r2);
            if (_involutionCache.TryGetValue(code, out var cached)) return cached;

            int[] inv;
            if (_calibratedPairs.TryGetValue(code, out var pair))
                inv = BuildInvolutionWithPair(pair.a, pair.b, code);
            else
                inv = BuildSeededInvolution(code);

            _involutionCache[code] = inv;
            return inv;
        }

        /// <summary>Involution that forces a↔b, then pairs the remaining 24 letters
        /// in a CONFIG-SEEDED order. Seeding the remainder (rather than fixing it
        /// in ascending order) ensures that even at the calibrated key, letters
        /// outside the forced pair still light different lamps as the rotors step —
        /// so the "same key, different output" lesson holds everywhere.</summary>
        private static int[] BuildInvolutionWithPair(int a, int b, int seed)
        {
            var inv = new int[26];
            for (int i = 0; i < 26; i++) inv[i] = -1;
            inv[a] = b; inv[b] = a;

            var remaining = new List<int>(24);
            for (int i = 0; i < 26; i++) if (i != a && i != b) remaining.Add(i);

            // Xor the seed so the remainder ordering is decorrelated from the
            // seeded-involution path while staying fully deterministic.
            LcgShuffle(remaining, unchecked(seed ^ 0x5BD1E995));

            for (int i = 0; i + 1 < remaining.Count; i += 2)
            {
                int x = remaining[i], y = remaining[i + 1];
                inv[x] = y; inv[y] = x;
            }
            return inv;
        }

        /// <summary>Deterministic random perfect-matching involution from a seed.
        /// 26 is even, so every letter is paired (no letter maps to itself — the
        /// same no-self-encrypt property real Enigma had).</summary>
        private static int[] BuildSeededInvolution(int seed)
        {
            var order = new List<int>(26);
            for (int i = 0; i < 26; i++) order.Add(i);
            LcgShuffle(order, seed);

            var inv = new int[26];
            for (int i = 0; i + 1 < order.Count; i += 2)
            {
                int x = order[i], y = order[i + 1];
                inv[x] = y; inv[y] = x;
            }
            return inv;
        }

        /// <summary>In-place Fisher-Yates with a fully deterministic LCG (no
        /// System.Random, so results are byte-identical across platforms / .NET
        /// versions — important for a cipher whose output must be reproducible).</summary>
        private static void LcgShuffle(List<int> list, int seed)
        {
            uint s = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
            for (int i = list.Count - 1; i > 0; i--)
            {
                s = unchecked(s * 1664525u + 1013904223u);
                int j = (int)(s % (uint)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ----------------------------------------------------------- odometer

        private static void Step(ref int r0, ref int r1, ref int r2)
        {
            // Rotor 2 is fastest; carry left on wrap.
            r2++;
            if (r2 >= 26) { r2 = 0; r1++; if (r1 >= 26) { r1 = 0; r0++; if (r0 >= 26) r0 = 0; } }
        }

        private static int Pack(int r0, int r1, int r2) => r0 * 676 + r1 * 26 + r2;
        private static int Wrap(int v) => ((v % 26) + 26) % 26;

        private static string Sanitise(string s, int len, string fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                char up = char.ToUpperInvariant(c);
                if (up >= 'A' && up <= 'Z') sb.Append(up);
            }
            string clean = sb.ToString();
            return clean.Length == 0 ? fallback : clean;
        }
    }
}
