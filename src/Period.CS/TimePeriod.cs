using System;
using System.Runtime.InteropServices;

namespace Period
{
    /// <summary>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimePeriod : IComparable<TimePeriod>, IComparable {

        internal long _value;

        internal TimePeriod(long value)
        {
            _value = value;
        }

        public int CompareTo(TimePeriod other)
        {
            return _value.CompareTo(other._value);
        }

        public int CompareTo(object other) {
            if (other is TimePeriod) {
                return _value.CompareTo(((TimePeriod)other)._value);
            }
            throw new ArgumentException("other", "Cannot compare values of different types")
                ;
        }

        public override bool Equals(object obj) {
            if (obj is TimePeriod)
            {
                return _value == ((TimePeriod) obj)._value;
            }
            return false;
        }

        // TODO pretty
        public override string ToString() { 
            return _value.ToString();
        }

        public override int GetHashCode() {
            return _value.GetHashCode();
        }

        public static explicit operator long(TimePeriod period) {
            return period._value;
        }

        public static explicit operator TimePeriod(long value)
        {
            return new TimePeriod(value);
        }

        // direct port from F#
        internal static class TimePeriodModule {

        }

    }
}