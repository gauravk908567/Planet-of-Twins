namespace PoT.Diagnostics
{
    /// <summary>One key event in the trail that leads up to a bug: what happened, when, and in which scene.</summary>
    public readonly struct Breadcrumb
    {
        /// <summary>Real seconds since the session started.</summary>
        public readonly double Time;
        /// <summary>Frame number, or -1 when recorded off the main thread.</summary>
        public readonly int Frame;
        public readonly string Category;
        public readonly string Text;
        /// <summary>The active scene when it was recorded ("?" off the main thread).</summary>
        public readonly string Scene;

        public Breadcrumb(double time, int frame, string category, string text, string scene)
        {
            Time = time;
            Frame = frame;
            Category = category;
            Text = text;
            Scene = scene;
        }
    }
}
