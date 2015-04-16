namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Diagnostics
open System.Runtime.InteropServices


// TODO
// Parsing:
// Two formats:
// Quick human format: 2015Q2 - second quarter of 2015, 2014W52, 2015M3, 2013H2...
// Long precise format: Q:201503 - a quarter ended at [the end of] March 2015, same as 1Q:20150331
//                      2015:Q - a quarter started at [the beginning of] 2015, same as 20150101:1Q
// Colon devides [start]:[end], with offsets like 1Q or 7D applied to another part

// Sequence of period could be preented as 20150101:D:2Q - daily data for the first two quarters of 2015



/// Base unit of TimePeriod
type UnitPeriod =
  | Tick = -1         //               100 nanosec
  // Unused zero value
  | Millisecond = 1   //              10 000 ticks
  | Second = 2        //          10 000 000 ticks
  | Minute = 3        //         600 000 000 ticks
  | Hour = 4          //      36 000 000 000 ticks
  | Day = 5           //     864 000 000 000 ticks
  | Month = 6         //                  Variable
  /// Static or constant
  | Eternity = 7      //                  Infinity

#nowarn "9" // no overlap of fields here
[<CustomComparison;CustomEquality;StructLayout(LayoutKind.Sequential)>]
type Period =
  struct
    val internal value : int64
    internal new(value:int64) = {value = value}
  end
  override x.Equals(yobj) =
    match yobj with
    | :? Period as y -> (x.value = y.value)
    | _ -> false
  override x.GetHashCode() = x.value.GetHashCode()
  override x.ToString() = x.value.ToString()
  interface System.IComparable<Period> with
    member x.CompareTo y = x.value.CompareTo(y.value)
  interface System.IComparable with
    member x.CompareTo other = 
      match other with
      | :? Period as y -> x.value.CompareTo(y.value)
      | _ -> invalidArg "other" "Cannot compare values of different types"

  static member op_Explicit(value:int64) : Period =  Period(value)
  static member op_Explicit(timePeriod:Period) : int64  = timePeriod.value
  

module internal TimePeriodModule =

  [<Literal>]
  let tickFlagValue = 0L
  [<Literal>]
  let zeroYear = 1 // 0001

  [<Literal>]
  let ticksPerMillisecond = 10000L
  [<Literal>]
  let ticksPerSecond = 10000000L
  [<Literal>]
  let ticksPerMinute = 600000000L
  [<Literal>]
  let ticksPerHour = 36000000000L
  [<Literal>]
  let ticksPerDay = 864000000000L

  [<Literal>]
  let msecPerDay = 86400000L
  [<Literal>]
  let msecPerHour = 3600000L
  [<Literal>]
  let msecPer15Min = 900000L
  [<Literal>]
  let msecPerMinute = 60000L
  [<Literal>]
  let msecPerSec = 1000L

  [<Literal>]
  let msecOffset = 0
  [<Literal>]
  let daysOffset = 27
  [<Literal>]
  let monthsOffset = 32
  [<Literal>]
  let lengthOffset = 49
  [<Literal>]
  let unitPeriodOffset = 59
  [<Literal>]
  let tickOffset = 0
  [<Literal>]
  let tickFlagOffset = 62

  [<Literal>]
  let ticksMask = 4611686018427387903L // ((1L <<< 62) - 1L) <<< 0
  [<Literal>]
  let msecMask = 134217727L // ((1L <<< 27)  - 1L) <<< 0
  [<Literal>]
  let msecMaskWithUnused = 134217727L // ((1L <<< (27+0))  - 1L)
  [<Literal>]
  let daysMask = 4160749568L // ((1L <<< 5) - 1L) <<< 27
  [<Literal>]
  let monthsMask =  562945658454016L // ((1L <<< 17) - 1L) <<< 32
  [<Literal>]
  let lengthMask = 575897802350002176L // ((1L <<< 10) - 1L) <<< 49
  [<Literal>]
  let unitPeriodMask = 4035225266123964416L // ((1L <<< 3) - 1L) <<< 59
  

  let inline getMsecInDay value = (value &&& msecMask) >>> msecOffset
  let inline setMsecInDay msecs value = 
    (msecs <<< msecOffset) ||| (value &&& ~~~msecMask)

  let inline getDays value = (value &&& daysMask) >>> daysOffset
  let inline setDays days value = (days <<< daysOffset) ||| (value &&& ~~~daysMask)

  let inline getMonths value = (value &&& monthsMask) >>> monthsOffset
  let inline setMonths months value = (months <<< monthsOffset) ||| (value &&& ~~~monthsMask)

  let inline getLength value = (value &&& lengthMask) >>> lengthOffset
  let inline setLength length value = (length <<< lengthOffset) ||| (value &&& ~~~lengthMask)

  let inline isTick (value) : bool = (tickFlagValue = (value >>> tickFlagOffset))
  let inline getTicks (value) = (value &&& ticksMask) >>> tickOffset
  let inline setTicks ticks value = (ticks <<< tickOffset) ||| (value &&& ~~~ticksMask)

  let inline getUnitPeriod (value:int64) : int64 = 
    if isTick value then (int64 UnitPeriod.Tick)
    else (value &&& unitPeriodMask) >>> unitPeriodOffset
  let inline setUnitPeriod unitPeriod value = 
    if isTick value then value
    else (unitPeriod <<< unitPeriodOffset) ||| (value &&& ~~~unitPeriodMask) 


  let inline getPeriod value = (value &&& (lengthMask ||| unitPeriodMask )) >>> lengthOffset
  let inline setPeriod period value = (period <<< lengthOffset) ||| (value &&& ~~~(lengthMask ||| unitPeriodMask ))



  let inline milliseconds (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    getMsecInDay(tpv) % 1000L
  let inline seconds (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerSec) % 60L
  let inline minutes (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerMinute) % 60L
  let inline hours (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerHour) % 24L
  let inline days (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getDays(tpv)) + 1L
  // 1 based like in calendar
  let inline months (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getMonths(tpv) % 12L) + 1L
  let inline years (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getMonths(tpv) / 12L) + (int64 zeroYear)
  let inline length (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    getLength(tpv)
  let inline unitPeriod (tpv:int64) : UnitPeriod =
    LanguagePrimitives.EnumOfValue <| int (getUnitPeriod tpv)


  /// Assume that inputs are not checked for logic
  let inline ofPartsUnsafe // TODO make a copy of it ofPartsSafe with arg checks and use it in the ctors.
    (unitPeriod:UnitPeriod) (length:int) 
    (year:int) (month:int) (day:int) 
    (hour:int) (minute:int) (second:int) (millisecond:int) : int64 =
      match unitPeriod with
      | UnitPeriod.Tick -> 
        // first two bits are zero, then just UTC ticks
        DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc).Ticks
//        let mutable value : int64 = tickFlagValue <<< tickFlagOffset
//        value <- value |> setTicks (DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc).Ticks) // (... - zeroTicks)
//        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64((year - zeroYear) * 12 + (month-1)))
        value <- value |> setDays (int64(day - 1))
        let millisInDay = 
          (int64 hour) * msecPerHour + (int64 minute) * msecPerMinute 
          + (int64 second) * msecPerSec + (int64 millisecond)
        value <- value |> setMsecInDay millisInDay
        value
        

  // TODO WTF? I do not understand my own code here on the second re-read

  /// Convert datetime to TimePeriod with Windows built-in time zone infos
  /// Windows updates TZ info with OS update patches, could also use NodaTime for this
  let inline ofStartDateTimeWithZoneUnsafe (unitPeriod:UnitPeriod) (length:int)  (startDate:DateTime) (tzi:TimeZoneInfo) =
    // number of 30 minutes intervals, with 24 = UTC/zero offset
    // TODO test with India
    let startDtUtc =  DateTimeOffset(startDate,tzi.GetUtcOffset(startDate))
    match unitPeriod with
      | UnitPeriod.Tick -> 
        startDtUtc.Ticks
//        let mutable value : int64 = tickFlagValue <<< tickFlagOffset
//        value <- value |> setTicks startDtUtc.Ticks // (startDtUtc.Ticks - zeroTicks)
//        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64 <| (startDtUtc.Year - zeroYear) * 12 + (startDtUtc.Month - 1))
        value <- value |> setDays (int64 <| startDtUtc.Day - 1)
        value <- value |> setMsecInDay (int64 <| startDtUtc.TimeOfDay.TotalMilliseconds)
        value

  let inline ofStartDateTimeOffset (unitPeriod:UnitPeriod) (length:int)  (startDto:DateTimeOffset) =
    match unitPeriod with
      | UnitPeriod.Tick -> 
        startDto.UtcTicks
//        let mutable value : int64 = tickFlagValue <<< tickFlagOffset
//        value <- value |> setTicks startDto.UtcTicks //(startDto.UtcTicks - zeroTicks)
//        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64 <| (startDto.UtcDateTime.Year - zeroYear) * 12 + (startDto.UtcDateTime.Month - 1))
        value <- value |> setDays (int64 <| startDto.UtcDateTime.Day - 1)
        value <- value |> setMsecInDay (int64 <| startDto.UtcDateTime.TimeOfDay.TotalMilliseconds)
        value
            

  let inline addPeriods (numPeriods:int64) (tpv:int64) : int64 =
    if numPeriods = 0L then tpv
    else
      let unit = unitPeriod tpv
      let len = getLength tpv
      let addDay (numDays:int64) (tpv':int64) : int64 =
        let date = DateTime(int <| years tpv', int <|months tpv', int <| days tpv').AddDays(float numDays)
        let withDays = setDays (int64 <| date.Day - 1) tpv'
        setMonths (int64 <| (date.Year - zeroYear) * 12 + (date.Month - 1)) withDays
      let addIntraDay (numPeriods':int64) (tpv':int64) (multiple:int64) : int64 =
        let msecs = (getMsecInDay tpv') + numPeriods'*len*multiple
        if msecs >= msecPerDay then
          let msecsInDay = msecs % msecPerDay
          let numDays = msecs / msecPerDay
          let withMsecs = setMsecInDay msecsInDay tpv'
          addDay numDays withMsecs
        else if msecs < 0L then
          let msecsInDay = msecPerDay + (msecs % msecPerDay) // last part is negative, so (+)
          let numDays = (msecs / msecPerDay) - 1L
          let withMsecs = setMsecInDay msecsInDay tpv'
          addDay numDays withMsecs
        else
          setMsecInDay msecs tpv'
      match unit with
      | UnitPeriod.Tick -> 
        let ticks = (getTicks tpv) + numPeriods
        setTicks ticks tpv
      | UnitPeriod.Millisecond -> addIntraDay numPeriods tpv 1L
      | UnitPeriod.Second -> addIntraDay numPeriods tpv msecPerSec
      | UnitPeriod.Minute -> addIntraDay numPeriods tpv msecPerMinute
      | UnitPeriod.Hour -> addIntraDay numPeriods tpv msecPerHour     
      | UnitPeriod.Day -> addDay numPeriods tpv
      | UnitPeriod.Month ->
          let months = (getMonths tpv) + len * numPeriods
          setMonths months tpv
      | UnitPeriod.Eternity ->  tpv
      | _ -> failwith "wrong unit period, never hit this"

  let inline periodStart (tpv:int64) : DateTimeOffset =
    if isTick tpv then DateTimeOffset(getTicks tpv, TimeSpan.Zero)
    else 
      DateTimeOffset(
        int <| years tpv, 
        int <| months tpv, 
        int <| days tpv,0,0,0,0,
        TimeSpan.Zero).AddMilliseconds(float <| getMsecInDay tpv)

  /// period end is the start of the next period, exclusive (epsilon to the start of the next period)
  let inline periodEnd (tpv:int64) : DateTimeOffset =
    if isTick tpv then DateTimeOffset(getTicks tpv, TimeSpan.Zero)
    else 
      periodStart (addPeriods 1L tpv)

  let inline timeSpan (tpv:int64) : TimeSpan =
    if isTick tpv then TimeSpan(1L)
    else TimeSpan((periodEnd tpv).Ticks - (periodStart tpv).Ticks)

  // the bigger a period the less important grouping becomes for a single series (but still needed if there are many short series)
  // because the total number of points is limited (e.g. in 10 years there are 86,400 hours ~ 10,800 buckets by 8 trading hours or 3,600 buckets by 24 hours)
  // and the bigger bucket density should be expected
  // 100 000 buckets take 2.8 Mb (key + pointer + array overhead per bucket)
  // 1Gb of data = 100 Mn items in buckets (10 bytes per item: 2 key + 8 value)
  // 1Gb ~ 10 seconds of data per every tick, 1526 buckets 43kb
  // 1Gb ~ 28 hours of millisecondly data, 1667 buckets 47kb
  // 1Gb ~ 38 months of secondly data, 27778 buckets 0.78Mb
  // 1Gb ~ 192 years of minutely data, 69444 buckets 1.94Mb

  let bucketHash (tpv:int64) (targetUnit:UnitPeriod): int64 = //TODO inline
    let periodOnly tpv' = (tpv' &&& (~~~(msecMask ||| daysMask ||| monthsMask)))
    let originalUnit = unitPeriod tpv
    if targetUnit < originalUnit then invalidOp "Cannot map period to smaller base periods than original"
    if targetUnit > originalUnit then 
      Printf.sprintf "%s" ("target" + (int targetUnit).ToString()) |> ignore
      Printf.sprintf "%s" ("originalUnit" +  (int originalUnit).ToString()) |> ignore
      raise (NotImplementedException("TODO targetUnit > originalUnit"))
    match targetUnit with
    | UnitPeriod.Tick ->
      // group ticks by second
      (((( (tpv &&& ticksMask) >>> tickOffset) / ticksPerSecond) * ticksPerSecond) <<< tickOffset) ||| (1L <<< tickFlagOffset)
    | UnitPeriod.Millisecond ->
      // group by second; 1000 ms in a second
      (tpv &&& ~~~msecMaskWithUnused) ||| ( ((getMsecInDay tpv)/(msecPerSec)) * msecPerSec  )
    | UnitPeriod.Second ->
      // group by 15 minutes; 900 seconds in 15 minutes
      (tpv &&& ~~~msecMaskWithUnused) ||| ( ((getMsecInDay tpv)/(msecPer15Min)) * msecPer15Min )
    | UnitPeriod.Minute ->
      // group by day; 1440 minutes in a full day, 600 minutes in 10 hours
      (tpv &&& ~~~msecMaskWithUnused) // NB: different with Hours because period bits are different
    | UnitPeriod.Hour ->
      // group by month, max 744 hours in a month
      (tpv &&& ~~~(msecMaskWithUnused ||| daysMask))
    | UnitPeriod.Day -> 
      // group by month
      (tpv >>> monthsOffset) <<< monthsOffset
    | _ -> 
      // months are all in one place
      periodOnly (tpv)



  // tpv2 - tpv1
  let rec intDiff (tpv2:int64) (tpv1:int64): int64 =
    if tpv1 > tpv2 then
      -(intDiff tpv1 tpv2)
    else
      let originalUnit1 = unitPeriod tpv1
      let originalUnit2 = unitPeriod tpv2
      if originalUnit1 <> originalUnit2 then raise (new ArgumentException("TimePeriods must have same unit periods to calcualte intDiff"))
      match originalUnit1 with
      | UnitPeriod.Tick ->
        (getTicks tpv2) - (getTicks tpv1)
      | UnitPeriod.Millisecond ->
        if (getMonths tpv1) <> (getMonths tpv2) then // slow
          let dt1 = periodStart tpv1
          let dt2 = periodStart tpv2
          let diff = dt2.Ticks - dt1.Ticks
          diff / ticksPerMillisecond
        else
          ((getMsecInDay tpv2) - (getMsecInDay tpv1)) 
          + ((getDays tpv2) - (getDays tpv1))*msecPerDay
      | UnitPeriod.Second ->
        if (getMonths tpv1) <> (getMonths tpv2) then // slow
          let dt1 = periodStart tpv1
          let dt2 = periodStart tpv2
          let diff = dt2.Ticks - dt1.Ticks
          diff / ticksPerSecond
        else
          (((getMsecInDay tpv2) - (getMsecInDay tpv1)) 
          + ((getDays tpv2) - (getDays tpv1))* msecPerDay) / msecPerSec
      | UnitPeriod.Minute ->
        if (getMonths tpv1) <> (getMonths tpv2) then // slow
          let dt1 = periodStart tpv1
          let dt2 = periodStart tpv2
          let diff = dt2.Ticks - dt1.Ticks
          diff / ticksPerMinute
        else
          (((getMsecInDay tpv2) - (getMsecInDay tpv1)) 
          + ((getDays tpv2) - (getDays tpv1))* msecPerDay) / msecPerMinute
      | UnitPeriod.Hour ->
        if (getMonths tpv1) <> (getMonths tpv2) then // slow
          let dt1 = periodStart tpv1
          let dt2 = periodStart tpv2
          let diff = dt2.Ticks - dt1.Ticks
          diff / ticksPerHour
        else
          (((getMsecInDay tpv2) - (getMsecInDay tpv1)) 
          + ((getDays tpv2) - (getDays tpv1))* msecPerDay) / msecPerHour
      | UnitPeriod.Day ->
        if (getMonths tpv1) <> (getMonths tpv2) then // slow
          let dt1 = periodStart tpv1
          let dt2 = periodStart tpv2
          let diff = dt2.Ticks - dt1.Ticks
          diff / ticksPerDay
        else
          (getDays tpv2) - (getDays tpv1)
      | _ -> 
        (getMonths tpv2) - (getMonths tpv1)




open TimePeriodModule


type Period with
  member this.Period with get(): UnitPeriod * int = unitPeriod this.value, int <| length this.value
  member this.Start with get(): DateTimeOffset = periodStart this.value
  member this.End with get() : DateTimeOffset = periodEnd this.value
  member this.TimeSpan with get() : TimeSpan = timeSpan this.value
  member this.Next with get() : Period = Period(addPeriods 1L this.value)
  member this.Previous with get() : Period = Period(addPeriods -1L this.value)

  /// This - other
  member this.Diff(other:Period) = intDiff this.value other.value
  member this.Add(diff:int64) = Period(addPeriods diff this.value)

  static member op_Explicit(timePeriod:Period) : DateTimeOffset = timePeriod.Start
  static member op_Explicit(timePeriod:Period) : DateTime = timePeriod.Start.DateTime


  static member (-) (tp1 : Period, tp2 : Period) : int64 = intDiff tp1.value tp2.value 

  static member Hash(tp:Period) : Period = Period(bucketHash (tp.value) (unitPeriod (tp.value)))


  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startTime:DateTimeOffset) =
    {value =
      ofStartDateTimeOffset unitPeriod (int numberOfUnitPeriods) startTime}
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startTime:DateTime, tzi:TimeZoneInfo) =
    {value =
      ofStartDateTimeWithZoneUnsafe unitPeriod (int numberOfUnitPeriods) startTime tzi}

  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int, startSecond:int, startMillisecond:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute startSecond startMillisecond
        }

  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int, startSecond:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute startSecond 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay 0 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth 0 0 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 1/1/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int) =
    {value =
      ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear 0 0 0 0 0 0
    }


type TimePeriodComparer() =
  member x.Compare(a:Period,b:Period) = a.value.CompareTo(b.value)
  interface IComparer<Period> with
    member x.Compare(a,b) = a.value.CompareTo(b.value)
