namespace CityHighways.Presentation
{
    /// <summary>
    /// Invalidates out-of-order responses for a single logical resource.
    /// The newest request or mutation wins; older callbacks must not write
    /// cached state or clear the newer request's busy flag.
    /// </summary>
    public sealed class SocialRequestGate
    {
        int _revision;

        public int Begin()
        {
            unchecked { _revision++; }
            if (_revision == 0) _revision = 1;
            return _revision;
        }

        public bool IsCurrent(int revision) => revision != 0 && revision == _revision;

        public void Invalidate() => Begin();
    }
}
