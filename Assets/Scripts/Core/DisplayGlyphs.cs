using System;

namespace ChessFight.Network
{
    // Which letters the display face (Black Han Sans, Resources/Fonts) has, read from its cmap.
    // It has the 2,350 common Hangul syllables plus a few more, so a rare syllable a player may
    // type in a name (떄, 쀍 ...) is not there. Unity then takes that one letter from the next
    // font in the face's list (맑은 고딕) and the name looks broken: one thin letter among bold
    // ones (user 11-23, "권등떄이"). A text with such a letter is better drawn all in the body font.
    // Only letters count: punctuation the face lacks (·, ...) keeps falling back per glyph as before.
    // Regenerate both tables if the font file changes.
    public static class DisplayGlyphs
    {
        // Everything outside the Hangul syllable block, as inclusive [from, to] pairs.
        static readonly int[] Ranges =
        {
            0x0020, 0x005F, 0x0061, 0x007E, 0x00D7, 0x00D7, 0x00F7, 0x00F7, 0x2018, 0x2019,
            0x201C, 0x201D, 0x20A9, 0x20A9, 0x3131, 0x3163,
        };

        // One bit per syllable U+AC00..U+D7A3 (bit k&7 of byte k>>3), base64.
        const string HangulBits =
            "kw//PhmwAxMBKBARIACTBXseEbADl4E7EhGgAJOVazBRsAMRBTIwEbACEQErMHm4BhMBMBAAgAETASsQERAAk4crGAAAAJMFe3RR" +
            "sCOTATtwMAAAAAAAcBGwKxMAKRARgCMBAAAwHbAuAwEwMAAAAxEBIxAAAAATgWsQEQADEwETEBEwAAEBCDBVuCIAAAAwEbAClwf7" +
            "OhmwA5MBKxAAAACbDTs8GbADkwEzEQEAAhMHKx4RAAEBABAQEbACEwErMDmwIgEAEBAAAAARAQMwGDACkwfrFBEQApMFK3T5uK8T" +
            "ATsQAQAAAAAAcNmwShMBOxARAAMRAAAwWbEqEQEAEAAAAREBKxAAAACTBSsQAAABAQAiEBGgAhEBITBZsAIBAAAwGbBHkwE7OBGw" +
            "AwAAAAAAAAATDTs4EbADAQAQAAAAABMBKzARAIABABAQAQAAAAAAMJEYAgAAABAAAAARASMAAAAAkwlrEBEwABEBKzAZsMeTATs0" +
            "AYACEAAAMB2wgxMBOzAZsAMRAAowGbACEwAgEAAAAREBKxARoAITASsQAAABEQAAMBGQAhMBKzARsGYAAAAwEbAC0wf7OhmwBxMB" +
            "IAAAAAATBWs4EbADEwG4GAAAABsHKxABAAMBABAQEaACEQEKcHmwohEBKhAAAAAVAQAQEZACEwEpEAAAAJMFu/L5sCKTATsyATAC" +
            "AAAAMFmwBpMBOzARsCMRAABwmbACEQA4EAAAARMBAxABEACTBysWEAARAQIAMBEAAhEBKTARsAIBAAAwWbAOkwU7OBGwAxMAAQAA" +
            "AACTATkQAAACAwA7AAAAABMBKwAAAAAAAAAQAAAAAwAgMBGQAgAAAAAAAAAAAAAQAAACEQEDAAAAABMBK7B5sCOTATswEbBCEwEh" +
            "8NmwQ5MBOzARsAMRASNwWbAiEwEgEBGQAREBKzARsFKTAasWAAABEwEhMBGwAhMBKTAxsBIAAAAwObhamwEzOBEwAwEAIAAAAAAT" +
            "BTMQERAAEQAgEAEAAJMFIzARAAEBABAQETAAAQAgMBEwAgEAERAAEAARAAAAAAACE4UrEREQABMBqzF/uHMTATswkbCiEQEyMHvw" +
            "VxMBK3DR8OORATtwebkKEwE7MQGwAxMBKzARsAKTB6swEbADEwErMBGwAhMBq3ARtP4RASswebh30wV7MBGwA1MBIRARIACTB2sw" +
            "EbACEQEzEAAAABMF6zgQoAIRADAQEbACEwAgMHGwAgEAEBAAAAATAQsQERAAEwUrEAAAAJMFazadsAcTATsQAQACAAAAMBGwAwEA" +
            "IBAAAAEAAAAwGbAOAwEQEAAAAREBAwACAAITASMQAAADAAAIEAAAAAEAABAAkAIAAAAwGbCGUwF7MBGwA1EBIQAAAAATATswEbAC" +
            "EQAQEAEAAhMBKzARAAIBAAAQEbACAQAhMBGwAgEAEDABgAARASsQERACEwErAAAAAJMHazEZsBITATsxAQASAAAAMBmwAxMBKxER" +
            "sAMBAAAwGbACEwEpEAAAAgEBABABAAATASswERACEQEgMBGwAhEBATAVMBIAAAAwEbASkwM7MBGwAwEAIAAAAAATDTswEbACEQAQ" +
            "EAEAAJMBKxQBAAABAAAQCYACAQAAMFGwAgEAEBAAgAATASMQERACkwULEBEwABMBK3BZsDOTATswAAAAAAAAMBGwAxMBKzARMAMB" +
            "AQowEbACAwAgAAAAABEAABARoACTBSsQAAACAAAAEBGQABEBKRARsAAAAAAwEbACEyErORmwgxEAKAAAAAAThSszEbCCEwE7EBEg" +
            "ABMhKzIRoAMTACgwEaACEQEqMBGyAhEBITARAAITASswEZAC0yMrEhEwAhMBKwg=";

        static byte[] hangul;

        public static bool Has(int codePoint)
        {
            if (codePoint >= 0xAC00 && codePoint <= 0xD7A3)
            {
                if (hangul == null) hangul = Convert.FromBase64String(HangulBits);
                int k = codePoint - 0xAC00;
                return (hangul[k >> 3] & (1 << (k & 7))) != 0;
            }
            for (int i = 0; i < Ranges.Length; i += 2)
                if (codePoint >= Ranges[i] && codePoint <= Ranges[i + 1]) return true;
            return false;
        }

        // False when the text has a letter the display face would have to borrow from another font.
        public static bool Covers(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            for (int i = 0; i < text.Length; i++)
            {
                int cp = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                    if (!Has(cp)) return false;
                    continue;
                }
                if (char.IsLetter(text[i]) && !Has(cp)) return false;
            }
            return true;
        }
    }
}