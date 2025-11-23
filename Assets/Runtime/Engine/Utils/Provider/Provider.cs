using System;

namespace Runtime.Engine.Utils.Provider
{
    /// <summary>
    /// Generische Provider-Basis für Lazy/Explizite Initialisierung einer Singleton-ähnlichen Instanz.
    /// Nutzt statische <see cref="Current"/> Referenz; erlaubt externen Aufbau via Callback.
    /// </summary>
    /// <typeparam name="TP">Konkreter Provider-Typ (Self-referential generic).</typeparam>
    public abstract class Provider<TP> where TP : Provider<TP>, new()
    {
        /// <summary>
        /// Aktuell aktive Provider Instanz.
        /// </summary>
        public static TP Current { get; private set; }

        /// <summary>
        /// Initialisiert mit bereits erzeugter Instanz und ruft Initializer Callback auf.
        /// </summary>
        public static void Initialize(TP provider, Action<TP> initializer)
        {
            Current = provider;
            initializer(Current);
        }

        /// <summary>
        /// Erstellt neue Instanz mit new() Constraint und ruft Initializer Callback auf.
        /// </summary>
        public static void Initialize(Action<TP> initializer)
        {
            Current = new TP();
            initializer(Current);
        }
    }
}