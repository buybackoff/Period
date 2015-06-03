using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Period {


    public enum UnitPeriod {
        Tick = -1,          //               100 nanosec
                            // Unused zero value
        Millisecond = 1,    //              10 000 ticks
        Second = 2,         //          10 000 000 ticks
        Minute = 3,         //         600 000 000 ticks
        Hour = 4,           //      36 000 000 000 ticks
        Day = 5,            //     864 000 000 000 ticks
        Month = 6,          //                  Variable
        /// Static or constant
        Eternity = 7,       //                  Infinity
    }
}
