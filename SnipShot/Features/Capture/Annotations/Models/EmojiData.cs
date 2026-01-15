using System.Collections.Generic;
using Windows.Foundation;

namespace SnipShot.Features.Capture.Annotations.Models
{
    /// <summary>
    /// Configuración y datos para la herramienta de emojis.
    /// Contiene las categorías de emojis y propiedades de estilo.
    /// </summary>
    public class EmojiData
    {
        #region Emoji Categories

        /// <summary>
        /// Emojis de caritas y expresiones
        /// </summary>
        public static readonly string[] Smileys = new[]
        {
            "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂",
            "🙂", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗",
            "😜", "🤪", "😎", "🤓", "🧐", "😏", "😌", "😴"
        };

        /// <summary>
        /// Emojis de gestos y manos
        /// </summary>
        public static readonly string[] Gestures = new[]
        {
            "👍", "👎", "👌", "✌️", "🤞", "🤟", "🤘", "🤙",
            "👈", "👉", "👆", "👇", "☝️", "✋", "🤚", "🖐️",
            "👋", "🤝", "👏", "🙌", "👐", "🤲", "💪", "✍️"
        };

        /// <summary>
        /// Emojis de símbolos y objetos
        /// </summary>
        public static readonly string[] Symbols = new[]
        {
            "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍",
            "⭐", "✨", "💫", "🔥", "💯", "✅", "❌", "❓",
            "❗", "⚠️", "🚫", "♻️", "💤", "🔔", "📌", "💡"
        };

        /// <summary>
        /// Emojis de celebración y diversión
        /// </summary>
        public static readonly string[] Celebration = new[]
        {
            "🎉", "🎊", "🎈", "🎁", "🏆", "🥇", "🥈", "🥉",
            "🎯", "🎮", "🎲", "🎭", "🎨", "🎬", "🎤", "🎧",
            "🎵", "🎶", "🎹", "🥁", "🎸", "🎺", "🎻", "🪘"
        };

        /// <summary>
        /// Emojis de naturaleza y clima
        /// </summary>
        public static readonly string[] Nature = new[]
        {
            "☀️", "🌙", "⭐", "🌟", "🌈", "☁️", "⛈️", "❄️",
            "🌸", "🌺", "🌻", "🌹", "🌷", "🌱", "🌲", "🌴",
            "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼"
        };

        /// <summary>
        /// Emojis de flechas y direcciones
        /// </summary>
        public static readonly string[] Arrows = new[]
        {
            "⬆️", "➡️", "⬇️", "⬅️", "↗️", "↘️", "↙️", "↖️",
            "↕️", "↔️", "🔄", "🔃", "🔀", "🔁", "🔂", "▶️",
            "⏩", "⏭️", "⏯️", "◀️", "⏪", "⏮️", "🔼", "🔽"
        };

        /// <summary>
        /// Diccionario con todas las categorías
        /// </summary>
        public static readonly Dictionary<string, string[]> Categories = new()
        {
            { "smileys", Smileys },
            { "gestures", Gestures },
            { "symbols", Symbols },
            { "celebration", Celebration },
            { "nature", Nature },
            { "arrows", Arrows }
        };

        /// <summary>
        /// Iconos representativos de cada categoría
        /// </summary>
        public static readonly Dictionary<string, string> CategoryIcons = new()
        {
            { "smileys", "😀" },
            { "gestures", "👍" },
            { "symbols", "❤️" },
            { "celebration", "🎉" },
            { "nature", "🌸" },
            { "arrows", "➡️" }
        };

        /// <summary>
        /// Nombres de categorías para tooltips
        /// </summary>
        public static readonly Dictionary<string, string> CategoryNames = new()
        {
            { "smileys", "Caritas" },
            { "gestures", "Gestos" },
            { "symbols", "Símbolos" },
            { "celebration", "Celebración" },
            { "nature", "Naturaleza" },
            { "arrows", "Flechas" }
        };

        #endregion

        #region Emoji Settings

        /// <summary>
        /// Emoji seleccionado
        /// </summary>
        public string Emoji { get; set; } = "😀";

        /// <summary>
        /// Tamaño de fuente del emoji
        /// </summary>
        public double FontSize { get; set; } = 48;

        /// <summary>
        /// Posición del emoji en el canvas
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Ancho del contenedor
        /// </summary>
        public double Width { get; set; } = 60;

        /// <summary>
        /// Alto del contenedor
        /// </summary>
        public double Height { get; set; } = 60;

        /// <summary>
        /// Ángulo de rotación en grados
        /// </summary>
        public double RotationAngle { get; set; } = 0;

        #endregion

        #region Default Settings

        /// <summary>
        /// Configuración por defecto para emojis
        /// </summary>
        public static EmojiData Default => new()
        {
            Emoji = "😀",
            FontSize = 48,
            Width = 60,
            Height = 60,
            RotationAngle = 0
        };

        #endregion

        #region Methods

        /// <summary>
        /// Crea una copia de la configuración actual
        /// </summary>
        public EmojiData Clone()
        {
            return new EmojiData
            {
                Emoji = Emoji,
                FontSize = FontSize,
                Position = Position,
                Width = Width,
                Height = Height,
                RotationAngle = RotationAngle
            };
        }

        #endregion
    }
}
