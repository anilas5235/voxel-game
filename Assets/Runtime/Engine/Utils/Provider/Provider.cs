using System;

namespace Runtime.Engine.Utils.Provider
{
    public abstract class Provider<TP> where TP : Provider<TP>, new()
    {
        public static TP Current { get; private set; }

        public static void Initialize(TP provider, Action<TP> initializer)
        {
            Current = provider;
            initializer(Current);
        }

        public static void Initialize(Action<TP> initializer)
        {
            Current = new TP();
            initializer(Current);
        }
    }
}