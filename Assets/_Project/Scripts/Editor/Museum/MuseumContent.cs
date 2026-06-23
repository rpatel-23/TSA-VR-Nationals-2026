// -----------------------------------------------------------------------------
//  MuseumContent.cs   (Editor)
//  DECRYPTED, A Walk Through the History of Secret Writing
//
//  The EDUCATIONAL HEART of the expanded museum. This is a single, reviewable
//  source of truth for every word the visitor reads, gallery identities, dozens
//  of secondary exhibits, codebreaker biographies, wall timelines and wayfinding
//  copy. Every entry is original, historically grounded museum-label prose
//  (Title / Date / Description / Significance). There is no placeholder text and
//  no lorem ipsum anywhere in this file.
//
//  MuseumBuilder reads this database and stamps it into the scene as display
//  cases, pedestals, framed panels, portraits and plaques. Keeping the copy here
//  (data, not scene YAML) means the entire curatorial script can be diffed,
//  proof-read and regenerated, exactly the "assets-as-code" ethos of the rest
//  of the project.
// -----------------------------------------------------------------------------

using Decrypted.Core;

namespace Decrypted.EditorTools
{
    /// <summary>How a secondary artifact should be presented physically.</summary>
    public enum CaseKind
    {
        Vitrine,     // glass display case on legs, artifact inside
        Pedestal,    // open plinth with a spotlit artifact on top
        WallPanel,   // framed infographic / document on the wall
        Tablet,      // low table case (manuscripts, maps) angled toward the viewer
        Relief       // wall-mounted relief / large object
    }

    /// <summary>A single secondary exhibit with full museum-label copy.</summary>
    public struct Exhibit
    {
        public string Id;            // stable slug used for GameObject names
        public string Title;
        public string Date;
        public string Description;
        public string Significance;
        public CaseKind Kind;
        public string Artifact;      // shape hint for the procedural artifact builder

        public Exhibit(string id, string title, string date, string desc,
                       string significance, CaseKind kind, string artifact)
        {
            Id = id; Title = title; Date = date; Description = desc;
            Significance = significance; Kind = kind; Artifact = artifact;
        }
    }

    /// <summary>A pioneer of cryptology, shown as a framed portrait + bio plate.</summary>
    public struct Figure
    {
        public string Name;
        public string Years;
        public string Role;
        public string Bio;

        public Figure(string name, string years, string role, string bio)
        {
            Name = name; Years = years; Role = role; Bio = bio;
        }
    }

    /// <summary>One node on a wall-mounted historical timeline.</summary>
    public struct TimelineNode
    {
        public string Year;
        public string Caption;
        public TimelineNode(string year, string caption) { Year = year; Caption = caption; }
    }

    /// <summary>Everything the builder needs to dress one named gallery.</summary>
    public struct Gallery
    {
        public string Key;            // palette / identity key
        public MuseumState State;
        public string Name;           // hanging sign + directory
        public string Subtitle;
        public string Intro;          // orientation plaque body
        public string HeroTitle;      // headline for the centerpiece
        public string HeroPlaque;     // copy for the centerpiece plaque
        public Exhibit[] Exhibits;
        public Figure[] Figures;
        public TimelineNode[] Timeline;
        public string[] Directory;    // wayfinding lines on directory signs
    }

    public static class MuseumContent
    {
        // The museum's outward identity (used on the entrance and atrium).
        public const string MuseumName = "THE DECRYPTED";
        public const string MuseumTagline = "MUSEUM OF CRYPTOLOGY";
        public const string Mission =
            "For three thousand years, people have hidden meaning in plain sight, " +
            "to protect a battle plan, a love letter, a fortune, a faith. This museum " +
            "follows that unbroken thread, from the first scrambled alphabets of the " +
            "ancient world to the mathematics that guards every message you send today. " +
            "Touch the machines. Break the codes. Decrypt history.";

        // ------------------------------------------------------------------ //
        //  GALLERY 1, ENTRANCE / WELCOME PAVILION  (Splash state)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Entrance = new Gallery
        {
            Key = "entrance",
            State = MuseumState.Splash,
            Name = "WELCOME",
            Subtitle = "Main Entrance & Visitor Pavilion",
            Intro =
                "Welcome to the Museum of Cryptology. Your journey runs forward in time, " +
                "one gallery at a time, Antiquity, the World Wars, the Digital Age, and " +
                "the Quantum Future. Each room holds a code for you to break with your own " +
                "hands. When you are ready, step onto the seal and begin.",
            HeroTitle = "BEGIN YOUR JOURNEY",
            HeroPlaque =
                "Reach out and press the seal to enter the museum. No reading list, no " +
                "audio wand, everything here is learned by doing.",
            Exhibits = new[]
            {
                new Exhibit("etiquette", "Visitor Guide", "Today",
                    "Please explore at your own pace. Lean in to read a label and it will " +
                    "brighten for you. Reach out to touch anything that invites your hand, " +
                    "the disks turn, the keys press, the doors open.",
                    "Every exhibit in this museum is hands-on. Curiosity is the only ticket.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("hours", "The Collection", "Est. 2026",
                    "Five galleries trace the history of secret writing across thirty " +
                    "centuries: the Gallery of Antiquity, the Hall of the World Wars, the " +
                    "Digital Age, and the Synthesis Hall of the Future.",
                    "One continuous story, told through objects you can hold.",
                    CaseKind.WallPanel, "panel"),
            },
            Figures = new Figure[0],
            Timeline = new TimelineNode[0],
            Directory = new[]
            {
                "GALLERY 1   Antiquity",
                "GALLERY 2   The World Wars",
                "GALLERY 3   The Digital Age",
                "GALLERY 4   The Quantum Future",
                "→   Orientation Rotunda ahead",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 2, ORIENTATION ROTUNDA  (Atrium state)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Atrium = new Gallery
        {
            Key = "atrium",
            State = MuseumState.Atrium,
            Name = "ROTUNDA",
            Subtitle = "Orientation & the Grand Timeline of Secrecy",
            Intro =
                "Cryptology is the science of two opposing arts: cryptography, the making " +
                "of codes, and cryptanalysis, the breaking of them. For most of history " +
                "they have raced each other, every unbreakable cipher eventually meets a " +
                "mind clever enough to crack it. Walk the timeline on these walls, then " +
                "choose a doorway and step into the story.",
            HeroTitle = "THE GRAND TIMELINE",
            HeroPlaque =
                "Three thousand years of secret writing, from a scrambled clay tablet to " +
                "the quantum key. Follow it around the room; the doorways open onto the " +
                "eras in order.",
            Exhibits = new[]
            {
                new Exhibit("kerckhoffs", "Kerckhoffs's Principle", "1883",
                    "Dutch linguist Auguste Kerckhoffs argued that a cipher should stay " +
                    "secure even if the enemy knows everything about the system, except " +
                    "the key. \"The design is not a secret,\" he wrote; \"the key is.\"",
                    "The founding rule of modern security: never rely on a secret method, " +
                    "only on a secret key. Every cipher in this museum is judged by it.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("cia-triad", "What Cryptography Protects", "Principle",
                    "Three guarantees underlie every code: Confidentiality, that only the " +
                    "intended reader can understand; Integrity, that the message was not " +
                    "altered; and Authentication, that the sender is who they claim to be.",
                    "These three promises are the reason cryptography matters, in 50 BCE " +
                    "and in your phone tonight.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("cipher-vs-code", "Codes vs. Ciphers", "Definitions",
                    "A code replaces whole words or ideas with symbols from a codebook " +
                    "(\"BLUEBIRD\" means \"attack at dawn\"). A cipher works at the level of " +
                    "individual letters, transforming each by a rule. This museum is, mostly, " +
                    "a museum of ciphers.",
                    "Knowing the difference is the first tool of the codebreaker.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("globe", "Where Secrets Were Kept", "Worldwide",
                    "Sparta, Rome, Baghdad, Florence, Paris, Bletchley, Stanford, Geneva, " +
                    "the art of the cipher belongs to no single nation. Each pin on this map " +
                    "marks a place where the history of secrecy turned.",
                    "Cryptology is one of humanity's truly global inheritances.",
                    CaseKind.Tablet, "map"),
            },
            Figures = new Figure[0],
            Timeline = new[]
            {
                new TimelineNode("1900 BCE", "Non-standard hieroglyphs, Egypt's first hidden writing"),
                new TimelineNode("600 BCE", "Atbash, Hebrew scribes reverse the alphabet"),
                new TimelineNode("500 BCE", "The Spartan scytale wraps a message around a staff"),
                new TimelineNode("50 BCE", "Julius Caesar shifts the alphabet by three"),
                new TimelineNode("850 CE", "Al-Kindi invents frequency analysis in Baghdad"),
                new TimelineNode("1467", "Alberti builds the first polyalphabetic cipher disk"),
                new TimelineNode("1586", "The Vigenère cipher, 'le chiffre indéchiffrable'"),
                new TimelineNode("1863", "Kasiski finally breaks Vigenère"),
                new TimelineNode("1917", "The Zimmermann Telegram is decrypted"),
                new TimelineNode("1932", "Rejewski's mathematics cracks Enigma"),
                new TimelineNode("1943", "Colossus, the first programmable electronic computer"),
                new TimelineNode("1976", "Diffie & Hellman publish public-key cryptography"),
                new TimelineNode("1977", "RSA turns the idea into a usable cipher"),
                new TimelineNode("2001", "AES is adopted as the world standard"),
                new TimelineNode("1984", "Bennett & Brassard propose quantum key distribution"),
                new TimelineNode("2024", "NIST publishes the first post-quantum standards"),
            },
            Directory = new[]
            {
                "◄ Gallery 1   ANTIQUITY",
                "    The scytale, Caesar & the birth of codebreaking",
                "",
                "Gallery 2   THE WORLD WARS ►",
                "    Enigma, Bletchley Park & the codebreakers",
                "",
                "Gallery 3   THE DIGITAL AGE ►",
                "Gallery 4   THE QUANTUM FUTURE ►",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 3, ANTIQUITY  (AncientRoom state, Caesar disk hero)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Ancient = new Gallery
        {
            Key = "ancient",
            State = MuseumState.AncientRoom,
            Name = "ANTIQUITY",
            Subtitle = "Ciphers of the Ancient & Renaissance World",
            Intro =
                "The oldest secrets were kept with the simplest tools: a staff, a shifted " +
                "alphabet, a turning disk. Yet within these galleries lies the whole drama " +
                "of cryptology, the first cipher, the first break, and the long duel " +
                "between them. Take the cipher disk at the centre and turn it until the " +
                "message makes sense.",
            HeroTitle = "THE CAESAR CIPHER DISK",
            HeroPlaque =
                "Julius Caesar protected his dispatches by shifting every letter three " +
                "places down the alphabet, A became D, B became E. Turn the inner ring " +
                "until 'FURVV WKH UXELFRQ' resolves. The shift you need is +3.",
            Exhibits = new[]
            {
                new Exhibit("scytale", "The Spartan Scytale", "c. 500 BCE",
                    "A strip of parchment wound around a wooden rod of an exact diameter. " +
                    "Written along the staff, then unwound, the letters scatter into " +
                    "nonsense; only a rod of the matching thickness lines them up again.",
                    "The earliest known military cipher, and the first transposition, " +
                    "hiding a message by reordering it rather than replacing its letters.",
                    CaseKind.Pedestal, "rod"),
                new Exhibit("atbash", "Atbash, The Mirror Alphabet", "c. 600 BCE",
                    "Hebrew scribes folded the alphabet against itself: the first letter " +
                    "for the last, the second for the second-last. The system appears " +
                    "hidden inside the Book of Jeremiah, where 'Sheshach' conceals 'Babel'.",
                    "One of the first substitution ciphers, and proof that secret writing " +
                    "was born alongside writing itself.",
                    CaseKind.Vitrine, "tablet"),
                new Exhibit("polybius", "The Polybius Square", "c. 150 BCE",
                    "The Greek historian Polybius arranged the alphabet in a 5×5 grid, so " +
                    "every letter became a pair of numbers, its row and column. Signalled " +
                    "with torches, it let messages cross valleys letter by letter.",
                    "It turned letters into numbers two thousand years before computers did " +
                    "the same, and underlies many later ciphers.",
                    CaseKind.Tablet, "grid"),
                new Exhibit("caesar-context", "Caesar's Cipher", "c. 50 BCE",
                    "The biographer Suetonius records that Caesar replaced each letter with " +
                    "the one three places further on. Augustus, his heir, preferred a shift " +
                    "of one. Simple, fast, and good enough against a largely illiterate foe.",
                    "The most famous cipher in history, and the namesake of every 'shift " +
                    "cipher' taught since.",
                    CaseKind.Pedestal, "scroll"),
                new Exhibit("alkindi", "Al-Kindi & the First Codebreak", "c. 850 CE",
                    "In the House of Wisdom in Baghdad, the polymath Al-Kindi noticed that " +
                    "every language uses some letters more than others. Count the symbols, " +
                    "match the most common to 'E' or its equivalent, and any simple " +
                    "substitution unravels.",
                    "Frequency analysis, the birth of cryptanalysis. For 600 years it made " +
                    "every monalphabetic cipher readable to a patient mind.",
                    CaseKind.Tablet, "manuscript"),
                new Exhibit("alberti", "The Alberti Cipher Disk", "1467",
                    "The Renaissance architect Leon Battista Alberti mounted two alphabet " +
                    "rings on a common pin and turned the inner one partway through a " +
                    "message, so the same plaintext letter enciphered differently each time.",
                    "The first polyalphabetic cipher. By moving the key mid-message, Alberti " +
                    "blunted frequency analysis and reset the duel between code and break.",
                    CaseKind.Pedestal, "disk"),
                new Exhibit("trithemius", "Trithemius' Tabula Recta", "1508",
                    "The abbot Johannes Trithemius published a square table of twenty-six " +
                    "alphabets, each shifted one further than the last, and stepped through " +
                    "them letter by letter, a new cipher alphabet for every position.",
                    "The table at the heart of nearly every Renaissance polyalphabetic " +
                    "cipher, including the one that would baffle Europe for three centuries.",
                    CaseKind.Vitrine, "book"),
                new Exhibit("vigenere", "The Vigenère Cipher", "1586",
                    "A keyword chooses a different alphabet from Trithemius' table for each " +
                    "letter of the message, then repeats. Misattributed to Blaise de " +
                    "Vigenère, it was so resistant to frequency analysis that Europe called " +
                    "it 'le chiffre indéchiffrable', the unbreakable cipher.",
                    "For 300 years it was the gold standard of secrecy, until Charles " +
                    "Babbage and Friedrich Kasiski found the crack in its repeating key.",
                    CaseKind.Vitrine, "tablet"),
                new Exhibit("babington", "The Babington Plot", "1586",
                    "Mary, Queen of Scots conspired from prison to overthrow Elizabeth I, " +
                    "her letters enciphered in a substitution alphabet of strange symbols. " +
                    "Elizabeth's spymaster intercepted them, and his codebreaker Thomas " +
                    "Phelippes read every word, then forged a postscript to expose the plotters.",
                    "Cryptanalysis as a matter of life and death: the broken cipher sent a " +
                    "queen to the executioner's block in 1587.",
                    CaseKind.Tablet, "letter"),
                new Exhibit("greatcipher", "The Great Cipher of Louis XIV", "c. 1690",
                    "Father-and-son cryptographers Antoine and Bonaventure Rossignol built a " +
                    "code of 587 numbers for the Sun King, some standing for syllables, " +
                    "some for letters, some as traps that deleted the previous symbol.",
                    "So strong that captured dispatches sat unread for 200 years, until " +
                    "Étienne Bazeries finally solved it in the 1890s.",
                    CaseKind.Vitrine, "ledger"),
                new Exhibit("voynich", "The Voynich Manuscript", "15th c.",
                    "A 240-page codex written in an unknown script, illustrated with " +
                    "impossible plants and astronomical charts. Codebreakers from both World " +
                    "Wars have tried; no one has read a single confirmed word.",
                    "Cryptology's greatest open mystery, proof that some secrets keep " +
                    "themselves.",
                    CaseKind.Tablet, "codex"),
            },
            Figures = new[]
            {
                new Figure("Al-Kindi", "c. 801–873", "Philosopher of Baghdad",
                    "Author of the first known treatise on breaking ciphers. His insight " +
                    "that letter frequencies betray a message founded the entire science of " +
                    "cryptanalysis."),
                new Figure("Leon Battista Alberti", "1404–1472", "The Renaissance Polymath",
                    "Architect, artist and 'father of Western cryptography.' His turning " +
                    "cipher disk introduced the polyalphabetic idea that would dominate for " +
                    "centuries."),
                new Figure("Charles Babbage", "1791–1871", "The Quiet Codebreaker",
                    "The computing pioneer secretly broke the Vigenère cipher around 1854, " +
                    "but never published, the credit went to Kasiski nine years later."),
            },
            Timeline = new[]
            {
                new TimelineNode("600 BCE", "Atbash"),
                new TimelineNode("500 BCE", "Scytale"),
                new TimelineNode("50 BCE", "Caesar shift"),
                new TimelineNode("850 CE", "Frequency analysis"),
                new TimelineNode("1467", "Alberti disk"),
                new TimelineNode("1586", "Vigenère cipher"),
                new TimelineNode("1863", "Vigenère broken"),
            },
            Directory = new[]
            {
                "GALLERY 1 · ANTIQUITY",
                "",
                "Centre   The Caesar Cipher Disk",
                "Left     Scytale · Atbash · Polybius",
                "Right    Alberti · Vigenère · Babington",
                "",
                "Exit ►   Gallery 2 · The World Wars",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 4, THE WORLD WARS  (WWIIRoom state, Enigma hero)
        // ------------------------------------------------------------------ //
        private static readonly Gallery WWII = new Gallery
        {
            Key = "wwii",
            State = MuseumState.WWIIRoom,
            Name = "THE WORLD WARS",
            Subtitle = "Machines, Codebreakers & the Secret War",
            Intro =
                "In the twentieth century the cipher became a machine, and breaking it " +
                "became an industry. Behind the front lines, mathematicians and engineers " +
                "fought a silent war whose victories stayed secret for fifty years. Set the " +
                "rotors of the Enigma to MAC and read the message it was built to hide.",
            HeroTitle = "THE ENIGMA MACHINE",
            HeroPlaque =
                "Germany trusted its most vital orders to the Enigma, three spinning " +
                "rotors that re-wired the alphabet with every keystroke, offering more " +
                "settings than there are atoms in your body. Set the rotors to MAC, type " +
                "XJQBVK, and watch the word TSANLC emerge.",
            Exhibits = new[]
            {
                new Exhibit("zimmermann", "The Zimmermann Telegram", "1917",
                    "Britain's Room 40 intercepted a coded German cable offering Mexico the " +
                    "American Southwest if it would join the war. The codebreakers read it, " +
                    "then disguised how, and handed it to Washington.",
                    "A single decrypted telegram helped bring the United States into the " +
                    "First World War. Codebreaking changed the map of the world.",
                    CaseKind.Tablet, "telegram"),
                new Exhibit("onetimepad", "The One-Time Pad", "1917 / 1949",
                    "Gilbert Vernam and Joseph Mauborgne combined each letter with a key of " +
                    "pure random characters, used exactly once and never repeated. Decades " +
                    "later Claude Shannon proved it mathematically unbreakable.",
                    "The only cipher in history proven to offer perfect secrecy, at the " +
                    "brutal cost of a key as long as every message you will ever send.",
                    CaseKind.Vitrine, "pad"),
                new Exhibit("rejewski", "Rejewski Breaks Enigma", "1932",
                    "Polish mathematician Marian Rejewski attacked Enigma not with " +
                    "linguistics but with the theory of permutations, reconstructing its " +
                    "secret wiring from intercepted traffic alone.",
                    "The first break of the military Enigma, achieved years before the war, " +
                    "and handed to Britain and France in 1939 as the tanks rolled in.",
                    CaseKind.Pedestal, "rotor"),
                new Exhibit("bomba", "The Bomba & the Bombe", "1938–1940",
                    "Rejewski's electro-mechanical 'bomba' searched Enigma settings " +
                    "automatically. At Bletchley Park, Alan Turing and Gordon Welchman " +
                    "rebuilt the idea as the Bombe, ranks of spinning drums testing " +
                    "thousands of wirings an hour.",
                    "Machines built to defeat machines, the first great triumph of " +
                    "automated codebreaking.",
                    CaseKind.Relief, "drums"),
                new Exhibit("bletchley", "Bletchley Park, Station X", "1939–1945",
                    "In a Victorian mansion north of London, nearly ten thousand people, " +
                    "two-thirds of them women, intercepted, sorted and broke Axis ciphers " +
                    "around the clock, sworn to a silence many kept for the rest of their lives.",
                    "Historians credit the work here with shortening the war by up to two " +
                    "years and saving countless lives.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("lorenz", "Lorenz & Colossus", "1943",
                    "Hitler's high command used the Lorenz cipher, even tougher than Enigma. " +
                    "To break it, engineer Tommy Flowers built Colossus from 1,600 vacuum " +
                    "tubes, the world's first programmable electronic digital computer.",
                    "The secret war gave birth to the computer age. Colossus stayed " +
                    "classified for thirty years, robbing its builders of the credit.",
                    CaseKind.Relief, "rack"),
                new Exhibit("navajo", "The Navajo Code Talkers", "1942–1945",
                    "U.S. Marines in the Pacific transmitted orders in Navajo, a language " +
                    "almost unwritten and spoken by few outside the Nation. Layered with a " +
                    "code of its own, it carried messages in seconds that machines needed " +
                    "hours to encipher.",
                    "The only major military code of the war that the enemy never broke, " +
                    "spoken by the very people America had tried to silence.",
                    CaseKind.Pedestal, "handset"),
                new Exhibit("purple", "PURPLE, Breaking Japan's Cipher", "1940",
                    "William Friedman's U.S. Army team reconstructed Japan's diplomatic " +
                    "cipher machine without ever seeing one, building a working analogue " +
                    "from intercepts alone. The intelligence was codenamed MAGIC.",
                    "Proof that a machine's secrets could be inferred from its output, a " +
                    "feat of pure reasoning that shaped the war in the Pacific.",
                    CaseKind.Vitrine, "machine"),
                new Exhibit("sigaba", "SIGABA, The Unbroken Cipher", "1940s",
                    "America's own cipher machine stepped its rotors irregularly, driven by " +
                    "a second bank of rotors, defeating the predictability that doomed " +
                    "Enigma. No wartime adversary is known to have read a single SIGABA message.",
                    "The quiet hero of Allied security, a reminder that the best cipher is " +
                    "the one the enemy never even realises they have failed to break.",
                    CaseKind.Pedestal, "machine"),
            },
            Figures = new[]
            {
                new Figure("Alan Turing", "1912–1954", "Father of Computer Science",
                    "Designed the Bombe that industrialised the Enigma break and laid the " +
                    "theoretical foundation of the computer. Prosecuted for his " +
                    "homosexuality in 1952; pardoned, far too late, in 2013."),
                new Figure("Marian Rejewski", "1905–1980", "The First to Break Enigma",
                    "The Polish mathematician whose permutation theory cracked Enigma in " +
                    "1932, years before Bletchley Park. His gift to the Allies in 1939 " +
                    "changed the course of the war."),
                new Figure("Joan Clarke", "1917–1996", "Cryptanalyst, Hut 8",
                    "One of the finest codebreakers at Bletchley Park, she worked the naval " +
                    "Enigma alongside Turing and was repeatedly promoted into roles women " +
                    "were officially barred from holding."),
                new Figure("Tommy Flowers", "1905–1998", "Builder of Colossus",
                    "A Post Office engineer who, doubted by his superiors, funded much of " +
                    "the first electronic computer himself. He was ordered to destroy his " +
                    "machines and burn the plans at war's end."),
            },
            Timeline = new[]
            {
                new TimelineNode("1917", "Zimmermann Telegram"),
                new TimelineNode("1932", "Rejewski breaks Enigma"),
                new TimelineNode("1939", "Bletchley Park opens"),
                new TimelineNode("1940", "The Turing–Welchman Bombe"),
                new TimelineNode("1942", "Navajo Code Talkers"),
                new TimelineNode("1943", "Colossus runs"),
            },
            Directory = new[]
            {
                "GALLERY 2 · THE WORLD WARS",
                "",
                "Centre   The Enigma Machine",
                "Left     Zimmermann · Rejewski · the Bombe",
                "Right    Lorenz & Colossus · Navajo · PURPLE",
                "Wall     The Codebreakers of Bletchley Park",
                "",
                "Exit ►   Gallery 3 · The Digital Age",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 5, THE DIGITAL AGE  (VaultRoom state, Vault hero)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Vault = new Gallery
        {
            Key = "vault",
            State = MuseumState.VaultRoom,
            Name = "THE DIGITAL AGE",
            Subtitle = "The Mathematics That Guards the Modern World",
            Intro =
                "After the war, cryptography left the battlefield for the bank, the " +
                "browser and the phone. It also faced an old paradox: how can two strangers " +
                "agree on a secret key while an eavesdropper listens to every word? The " +
                "answer rebuilt the world. Recover the passphrase from the Enigma, TSANLC, " +
                "and open the vault.",
            HeroTitle = "THE DIGITAL SECURITY VAULT",
            HeroPlaque =
                "A modern secret is not hidden, it is locked, and only the holder of the " +
                "key can open it, even in full view of the world. Enter the word you " +
                "recovered next door, TSANLC, on the keypad to release the lock.",
            Exhibits = new[]
            {
                new Exhibit("shannon", "Shannon's Information Theory", "1948–1949",
                    "Claude Shannon turned secrecy into mathematics. He defined information " +
                    "in bits, proved exactly when a cipher is unbreakable, and showed that " +
                    "good ciphers must blend 'confusion' and 'diffusion' to hide every trace " +
                    "of the message.",
                    "The birth of the information age. Every cipher since is measured " +
                    "against the limits Shannon set.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("des", "DES, The First Public Standard", "1977",
                    "The U.S. government adopted the Data Encryption Standard so that banks " +
                    "and businesses could all encrypt the same way. Its 56-bit key, " +
                    "controversially short, was finally cracked by brute force in 1998 in " +
                    "just 56 hours.",
                    "The first cipher the whole world shared, and the moment cryptography " +
                    "became public infrastructure rather than a military secret.",
                    CaseKind.Vitrine, "chip"),
                new Exhibit("diffiehellman", "Diffie–Hellman Key Exchange", "1976",
                    "Whitfield Diffie and Martin Hellman solved the impossible: two people " +
                    "shouting across a crowded room can mix their colours of paint so that " +
                    "each ends with the same secret blend, while no listener can un-mix it.",
                    "The first public-key idea ever published, 'New Directions in " +
                    "Cryptography' ended four thousand years of needing to share a key in advance.",
                    CaseKind.Pedestal, "keys"),
                new Exhibit("rsa", "RSA, Public-Key Cryptography", "1977",
                    "Rivest, Shamir and Adleman gave everyone two keys: a public one that " +
                    "anyone can use to lock a message, and a private one that only you can " +
                    "use to unlock it. Its security rests on the difficulty of factoring " +
                    "enormous numbers.",
                    "The padlock of the internet. RSA lets total strangers exchange secrets " +
                    "safely, the foundation of e-commerce, email and secure login.",
                    CaseKind.Pedestal, "padlock"),
                new Exhibit("publickey", "The Locked Mailbox", "Concept",
                    "Imagine a mailbox with a slot: anyone can drop a letter in (the public " +
                    "key), but only the owner's key opens it to read (the private key). You " +
                    "never have to share the opening key to receive a secret.",
                    "This single metaphor explains why you can shop and bank online with " +
                    "people you have never met.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("aes", "AES, The World Standard", "2001",
                    "When DES grew weak, the world held an open contest. The winner, a " +
                    "Belgian design called Rijndael, became the Advanced Encryption Standard " +
                    ", fast, elegant, and approved to protect even top-secret information.",
                    "The cipher running quietly inside your phone, your wifi and your " +
                    "messages right now. No practical break is known.",
                    CaseKind.Vitrine, "chip"),
                new Exhibit("hash", "Hash Functions & Fingerprints", "1990s",
                    "A hash function crushes any file, a word or a library, into a short " +
                    "fingerprint. Change a single comma and the fingerprint changes utterly, " +
                    "yet you can never run it backwards to recover the original.",
                    "How your password is stored without being kept, and how the world " +
                    "checks that a file has not been tampered with.",
                    CaseKind.Pedestal, "prism"),
                new Exhibit("signatures", "Digital Signatures", "1980s–today",
                    "Run public-key cryptography in reverse and you can sign instead of " +
                    "hide: only your private key could have produced the mark, but anyone " +
                    "with your public key can verify it. The signature breaks if the " +
                    "document is altered.",
                    "Proof of who sent a message and that no one changed it, the backbone " +
                    "of software updates, contracts and digital identity.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("tls", "The Padlock in Your Browser", "1994–today",
                    "Every time a padlock appears in your address bar, your device and a " +
                    "distant server have performed a key exchange, agreed on a fresh secret, " +
                    "and switched to a fast cipher like AES, all in a fraction of a second.",
                    "The quiet ceremony, called TLS, that secures trillions of dollars and " +
                    "billions of conversations every single day.",
                    CaseKind.Relief, "padlock"),
                new Exhibit("cryptowars", "The Crypto Wars", "1990s",
                    "When strong encryption reached the public, governments tried to keep a " +
                    "key for themselves, the Clipper chip, and treated cipher software as " +
                    "a munition. Cryptographers fought back in court and in code, and the " +
                    "public's right to strong encryption largely prevailed.",
                    "The unfinished argument over privacy, security and power that still " +
                    "shapes technology law today.",
                    CaseKind.WallPanel, "panel"),
            },
            Figures = new[]
            {
                new Figure("Claude Shannon", "1916–2001", "Father of Information Theory",
                    "Proved the mathematical limits of secrecy and measured information in " +
                    "bits. His wartime work and his 1949 paper turned cryptography from an " +
                    "art into a science."),
                new Figure("Diffie & Hellman", "fl. 1976", "Public-Key Pioneers",
                    "Whitfield Diffie and Martin Hellman published the key-exchange that " +
                    "made secure communication possible between strangers, and shared the " +
                    "2015 Turing Award for it."),
                new Figure("Rivest · Shamir · Adleman", "fl. 1977", "The R, S and A of RSA",
                    "Three MIT researchers who turned the public-key dream into a working " +
                    "algorithm in a single intense night, and gave their initials to the " +
                    "cipher that secured the internet."),
            },
            Timeline = new[]
            {
                new TimelineNode("1949", "Shannon's secrecy paper"),
                new TimelineNode("1976", "Diffie–Hellman"),
                new TimelineNode("1977", "RSA & DES"),
                new TimelineNode("1991", "Encryption goes public (PGP)"),
                new TimelineNode("2001", "AES standard"),
                new TimelineNode("Today", "TLS secures the web"),
            },
            Directory = new[]
            {
                "GALLERY 3 · THE DIGITAL AGE",
                "",
                "Centre   The Security Vault",
                "Left     Shannon · DES · the Crypto Wars",
                "Right    Diffie–Hellman · RSA · AES",
                "",
                "Exit ►   Gallery 4 · The Quantum Future",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 6, THE QUANTUM FUTURE / SYNTHESIS  (RevealChamber state)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Reveal = new Gallery
        {
            Key = "reveal",
            State = MuseumState.RevealChamber,
            Name = "THE FUTURE",
            Subtitle = "Synthesis Hall & the Quantum Horizon",
            Intro =
                "You have travelled from a shifted alphabet to the mathematics that guards " +
                "the world. The duel is not over. A new kind of machine, the quantum " +
                "computer, threatens today's locks, even as quantum physics offers a key " +
                "that physics itself protects. The sculpture before you holds all three " +
                "eras at once. Watch it change.",
            HeroTitle = "THE SYNTHESIS",
            HeroPlaque =
                "Roman stele, clockwork rotor, circuit lattice, one form flowing into the " +
                "next. From Caesar's alphabet to modern security, cryptography protects " +
                "information by transforming meaning into secrets only the intended " +
                "recipient can reveal. The thread is unbroken. Now it is yours to carry.",
            Exhibits = new[]
            {
                new Exhibit("qkd", "Quantum Key Distribution", "1984",
                    "Charles Bennett and Gilles Brassard found a way to send a key as " +
                    "single particles of light. The laws of quantum physics forbid copying " +
                    "them, so any eavesdropper unavoidably disturbs the message and is caught.",
                    "Security guaranteed not by hard mathematics but by the laws of nature " +
                    "themselves. The first quantum-secured bank transfer ran in 2004.",
                    CaseKind.Pedestal, "photon"),
                new Exhibit("shor", "The Quantum Threat", "1994",
                    "Mathematician Peter Shor proved that a large enough quantum computer " +
                    "could factor huge numbers with ease, quietly dissolving the hard " +
                    "problem that RSA and much of today's encryption rely upon.",
                    "The reason the whole world is now racing to upgrade its cryptography " +
                    "before such a machine is built.",
                    CaseKind.WallPanel, "panel"),
                new Exhibit("postquantum", "Post-Quantum Cryptography", "2024",
                    "A new generation of ciphers builds its locks from mathematical problems " +
                    ", tangled lattices in high-dimensional space, that even a quantum " +
                    "computer cannot easily solve. In 2024 the first such standards were " +
                    "published for the world to adopt.",
                    "The next chapter of the three-thousand-year duel, being written right " +
                    "now, and the cipher that may guard your grandchildren's secrets.",
                    CaseKind.Vitrine, "lattice"),
                new Exhibit("privacy", "The Right to a Whisper", "Today",
                    "Encryption is no longer only a tool of generals and spies. It protects " +
                    "the journalist's source, the dissident's plea, the ordinary person's " +
                    "ordinary life. Every secret you keep relies on an unbroken chain that " +
                    "began with a Spartan staff.",
                    "Cryptology is, in the end, about freedom: the human right to think, " +
                    "speak and trust in private.",
                    CaseKind.WallPanel, "panel"),
            },
            Figures = new[]
            {
                new Figure("Bennett & Brassard", "fl. 1984", "Founders of Quantum Cryptography",
                    "Their BB84 protocol turned the strangeness of quantum mechanics into " +
                    "an unbreakable key, opening the field of quantum cryptography."),
                new Figure("Peter Shor", "b. 1959", "The Quantum Disruptor",
                    "His 1994 algorithm showed that a quantum computer could break the " +
                    "ciphers guarding the internet, a warning that reshaped the field."),
            },
            Timeline = new[]
            {
                new TimelineNode("1984", "BB84 quantum key"),
                new TimelineNode("1994", "Shor's algorithm"),
                new TimelineNode("2004", "First quantum-secured transfer"),
                new TimelineNode("2024", "Post-quantum standards"),
                new TimelineNode("?", "The next unbreakable cipher"),
            },
            Directory = new[]
            {
                "GALLERY 4 · THE QUANTUM FUTURE",
                "",
                "Centre   The Synthesis Sculpture",
                "Around   Quantum keys · the quantum threat",
                "         Post-quantum cryptography",
                "",
                "Thank you for visiting.",
            },
        };

        // ------------------------------------------------------------------ //
        //  GALLERY 7, THE HALL OF COMPLETION  (Complete state)
        // ------------------------------------------------------------------ //
        private static readonly Gallery Complete = new Gallery
        {
            Key = "complete",
            State = MuseumState.Complete,
            Name = "THANK YOU",
            Subtitle = "The Hall of Completion",
            Intro =
                "You have walked the whole history of secret writing, from a turning " +
                "Roman disk to the mathematics that guards the modern world. This final " +
                "hall gathers the three machines you mastered and the single idea that " +
                "binds them across three thousand years.",
            HeroTitle = "THE UNBROKEN THREAD",
            // Verbatim closing plaque (CLAUDE.md / README). Do not paraphrase.
            HeroPlaque =
                "From Caesar's alphabet shifts to modern digital security, cryptography " +
                "protects information by transforming meaning into secrets only the " +
                "intended recipient can reveal.",
            Exhibits = new[]
            {
                new Exhibit("recap-caesar", "The Caesar Cipher Disk", "Antiquity",
                    "You shifted the alphabet three places and watched a dead language " +
                    "speak again. The simplest idea in cryptology, and the seed of every " +
                    "cipher that followed.",
                    "Where the long duel between code and codebreaker began.",
                    CaseKind.Pedestal, "disk"),
                new Exhibit("recap-enigma", "The Enigma Machine", "The World Wars",
                    "You set three rotors to MAC and drew the word TSANLC out of noise. A " +
                    "machine that rewired the alphabet with every keystroke, and the " +
                    "codebreakers who beat it anyway.",
                    "Where breaking codes became an industry, and gave birth to the computer.",
                    CaseKind.Vitrine, "machine"),
                new Exhibit("recap-vault", "The Digital Vault", "The Digital Age",
                    "You spoke the word the Enigma had hidden and the vault opened. Modern " +
                    "secrets are not buried, they are locked, and only the key can open them " +
                    "in full view of the world.",
                    "Where cryptography became the quiet foundation of everyday life.",
                    CaseKind.Vitrine, "padlock"),
            },
            Figures = new Figure[0],
            Timeline = new[]
            {
                new TimelineNode("50 BCE", "Caesar shifts the alphabet"),
                new TimelineNode("1942", "Enigma meets its match"),
                new TimelineNode("1977", "Public-key opens the digital age"),
                new TimelineNode("Today", "The thread is yours to carry"),
            },
            Directory = new[]
            {
                "THE HALL OF COMPLETION",
                "",
                "Centre   The Unbroken Thread",
                "Wall     Caesar  ·  Enigma  ·  the Vault",
                "",
                "Thank you for visiting DECRYPTED.",
            },
        };

        /// <summary>Every gallery, in walkthrough order.</summary>
        public static readonly Gallery[] Galleries =
        {
            Entrance, Atrium, Ancient, WWII, Vault, Reveal, Complete,
        };

        /// <summary>Look up the authored gallery for a museum state (or null-ish empty).</summary>
        public static bool TryGet(MuseumState state, out Gallery gallery)
        {
            foreach (var g in Galleries)
            {
                if (g.State == state) { gallery = g; return true; }
            }
            gallery = default;
            return false;
        }
    }
}
