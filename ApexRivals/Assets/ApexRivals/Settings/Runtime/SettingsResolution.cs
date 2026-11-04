using System;

namespace ApexRivals.Settings.Runtime
{
    public readonly struct SettingsResolution : IEquatable<SettingsResolution>
    {
        public SettingsResolution(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public bool IsValid => Width > 0 && Height > 0;

        public bool Equals(SettingsResolution other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is SettingsResolution other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Width;
                hash = (hash * 397) ^ Height;
                return hash;
            }
        }

        public static bool operator ==(SettingsResolution left, SettingsResolution right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SettingsResolution left, SettingsResolution right)
        {
            return !left.Equals(right);
        }
    }
}
