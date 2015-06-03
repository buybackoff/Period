namespace Period

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
// Colon divides [start]:[end], with offsets like 1Q or 7D applied to another part

// Sequence of period could be printed as 20150101:D:2Q - daily data for the first two quarters of 2015


/// Base unit of a period
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


 

module internal TimePeriodModule =

  [<Literal>]
  let tickFlagValue = 0L
  [<Literal>]
  let nonTickFlagValue = 1L
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
  let msecMask = 562949953421311L // ((1L <<< 49)  - 1L) <<< 0
  [<Literal>]
  let lengthMask = 575897802350002176L // ((1L <<< 10) - 1L) <<< 49
  [<Literal>]
  let unitPeriodMask = 4035225266123964416L // ((1L <<< 3) - 1L) <<< 59
  

  let isTick (value) : bool = (tickFlagValue = (value >>> tickFlagOffset))
  let markNotTick value = value ||| (nonTickFlagValue <<< tickFlagOffset)

  let getTicks (value) = 
    if isTick value then (value &&& ticksMask) >>> tickOffset
    else (value &&& msecMask) * ticksPerMillisecond
      
  let setTicks ticks value = 
    if isTick value then (ticks <<< tickOffset) ||| (value &&& ~~~ticksMask)
    else 
      let msecs = ticks/ticksPerMillisecond
      (msecs <<< msecOffset) ||| (value &&& ~~~msecMask)

  let inline getStartDateTime value : DateTime = DateTime(getTicks value, DateTimeKind.Utc)

  let inline setStartDateTime (dt:DateTime) value : int64 = setTicks (dt.ToUniversalTime().Ticks) value

  let getLength value = (value &&& lengthMask) >>> lengthOffset
  let setLength length value = (length <<< lengthOffset) ||| (value &&& ~~~lengthMask)

  let getUnitPeriod (value:int64) : int64 = 
    if isTick value then (int64 UnitPeriod.Tick)
    else (value &&& unitPeriodMask) >>> unitPeriodOffset
  let setUnitPeriod unitPeriod value = 
    if isTick value then value
    else (unitPeriod <<< unitPeriodOffset) ||| (value &&& ~~~unitPeriodMask) 

  let getPeriod value = (value &&& (lengthMask ||| unitPeriodMask )) >>> lengthOffset
  let setPeriod period value = (period <<< lengthOffset) ||| (value &&& ~~~(lengthMask ||| unitPeriodMask ))



  let milliseconds (tpv:int64) : int64 = 
    (getStartDateTime tpv).Millisecond |> int64
  let seconds (tpv:int64) : int64 = 
    (getStartDateTime tpv).Second |> int64
  let minutes (tpv:int64) : int64 = 
    (getStartDateTime tpv).Minute |> int64
  let hours (tpv:int64) : int64 = 
    (getStartDateTime tpv).Hour |> int64
  let days (tpv:int64) : int64 = 
    (getStartDateTime tpv).Day |> int64
  let months (tpv:int64) : int64 = 
    (getStartDateTime tpv).Month |> int64
  let years (tpv:int64) : int64 = 
    (getStartDateTime tpv).Year |> int64
  let length (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    getLength(tpv)
  let unitPeriod (tpv:int64) : UnitPeriod =
    LanguagePrimitives.EnumOfValue <| int (getUnitPeriod tpv)


  /// Assume that inputs are not checked for logic
  let ofPartsUnsafe // TODO make a copy of it ofPartsSafe with arg checks and use it in the ctors.
    (unitPeriod:UnitPeriod) (length:int) 
    (year:int) (month:int) (day:int) 
    (hour:int) (minute:int) (second:int) (millisecond:int) : int64 =
      let startDtUtc = 
        DateTime(year, month, day, 
                  hour, minute, second, millisecond, DateTimeKind.Utc)
      match unitPeriod with
      | UnitPeriod.Tick -> 
        // first two bits are zero, then just UTC ticks
        startDtUtc.Ticks
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setStartDateTime startDtUtc
        value
        

  /// Convert datetime to TimePeriod with Windows built-in time zone infos
  /// Windows updates TZ info with OS update patches, could also use NodaTime for this
  let ofStartDateTimeWithZoneUnsafe (unitPeriod:UnitPeriod) (length:int)  (startDate:DateTime) (tzi:TimeZoneInfo) =
    // number of 30 minutes intervals, with 24 = UTC/zero offset
    // TODO test with India
    let startDto =  DateTimeOffset(startDate,tzi.GetUtcOffset(startDate))
    match unitPeriod with
      | UnitPeriod.Tick -> startDto.Ticks
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setStartDateTime startDto.UtcDateTime
        value

  let ofStartDateTimeOffset (unitPeriod:UnitPeriod) (length:int)  (startDto:DateTimeOffset) =
    match unitPeriod with
      | UnitPeriod.Tick -> startDto.UtcTicks
      | _ ->
        let mutable value : int64 = markNotTick 0L // nonTickFlagValue <<< tickFlagOffset
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setStartDateTime startDto.UtcDateTime
        value
            
  let inline compare (first:int64) (second:int64) =
    if getPeriod first = getPeriod second then first.CompareTo(second)
    else (getTicks first).CompareTo(getTicks second)
    // (getTicks first).CompareTo(getTicks second) // this is enough, try test only it

  let addPeriods (numPeriods:int64) (tpv:int64) : int64 =
    if numPeriods = 0L then tpv
    else
      let unit = unitPeriod tpv
      let len = getLength tpv
      let step = len * numPeriods
      match unit with
      | UnitPeriod.Tick ->
        let ticks = (getTicks tpv) + numPeriods
        setTicks ticks tpv
      | UnitPeriod.Millisecond -> 
        setStartDateTime ((getStartDateTime tpv).AddMilliseconds(float step)) tpv
      | UnitPeriod.Second -> 
        setStartDateTime ((getStartDateTime tpv).AddSeconds(float step)) tpv
      | UnitPeriod.Minute -> 
        setStartDateTime ((getStartDateTime tpv).AddMinutes(float step)) tpv
      | UnitPeriod.Hour -> 
        setStartDateTime ((getStartDateTime tpv).AddHours(float step)) tpv
      | UnitPeriod.Day -> 
        setStartDateTime ((getStartDateTime tpv).AddDays(float step)) tpv
      | UnitPeriod.Month ->
        setStartDateTime ((getStartDateTime tpv).AddMonths(int step)) tpv
      | UnitPeriod.Eternity -> tpv
      | _ -> failwith "wrong unit period, never hit this"

  let periodStart (tpv:int64) : DateTimeOffset =
    DateTimeOffset(getStartDateTime tpv)

  /// period end is the start of the next period, exclusive (epsilon to the start of the next period)
  let periodEnd (tpv:int64) : DateTimeOffset =
    // for ticks start and end are the same
    if isTick tpv then 
      DateTimeOffset(getTicks tpv, TimeSpan.Zero)
    else 
      periodStart (addPeriods 1L tpv)

  let timeSpan (tpv:int64) : TimeSpan =
    if isTick tpv then TimeSpan(1L)
    else TimeSpan((periodEnd tpv).Ticks - (periodStart tpv).Ticks)


  let bucketHash (tpv:int64) (targetUnit:UnitPeriod): int64 = //TODO inline
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
      setTicks (((getTicks tpv)/ticksPerSecond) * ticksPerSecond) tpv
    | UnitPeriod.Second ->
      // group by 15 minutes; 900 seconds in 15 minutes
      let round = ticksPerMinute * 15L
      setTicks (((getTicks tpv)/round) * round) tpv
    | UnitPeriod.Minute ->
      // group by day; 1440 minutes in a full day, 600 minutes in 10 hours (rarely we have 24 hours)
      let round = ticksPerDay
      setTicks (((getTicks tpv)/round) * round) tpv
    | UnitPeriod.Hour ->
      // group by month, max 744 hours in a month
      let startDt = getStartDateTime tpv
      let bucketDt = DateTime(startDt.Year, startDt.Month, 1)
      setStartDateTime bucketDt tpv
    | UnitPeriod.Day -> 
      // group by month
      let startDt = getStartDateTime tpv
      let bucketDt = DateTime(startDt.Year, startDt.Month, 1)
      setStartDateTime bucketDt tpv
    | UnitPeriod.Month -> 
      // months are all in one place
      tpv &&& (~~~msecMask)
    | _ -> failwith "wrong unit period, never hit this"


  // TODO this doesn't account for number of UP, assumes 1
  // tpv2 - tpv1
  let rec intDiff (tpv2:int64) (tpv1:int64): int64 =
    if tpv1 > tpv2 then
      -(intDiff tpv1 tpv2)
    else
      let originalUnit1 = unitPeriod tpv1
      let originalUnit2 = unitPeriod tpv2
      if originalUnit1 <> originalUnit2 then raise (new ArgumentException("TimePeriods must have same unit periods to calcualte intDiff"))
      let len1 = getLength tpv1
      let len2 = getLength tpv2
      if len1 <> len2 then raise (new ArgumentException("TimePeriods must have same legth to calcualte intDiff"))
      let tickDiff = (getTicks tpv2) - (getTicks tpv1)
      match originalUnit1 with
      | UnitPeriod.Tick -> tickDiff
      | UnitPeriod.Millisecond -> tickDiff / (ticksPerMillisecond * len1)
      | UnitPeriod.Second -> tickDiff / (ticksPerSecond * len1) 
      | UnitPeriod.Minute -> tickDiff / (ticksPerMinute * len1)
      | UnitPeriod.Hour -> tickDiff / (ticksPerHour * len1)
      | UnitPeriod.Day -> tickDiff / (ticksPerDay * len1)
      | _ -> 
        let dt1 = (getStartDateTime tpv1)
        let dt2 = (getStartDateTime tpv2)
        (int64 <| (dt2.Year * 12 + dt2.Month) - (dt1.Year * 12 + dt1.Month)) / len1


open TimePeriodModule



  member this.UnitPeriod with get(): UnitPeriod = unitPeriod this.value
  member this.Length with get(): int = int <| length this.value
  inherit IComparable
  /// IPeriod is compared by Start property
  inherit IComparable<IPeriod>
  /// Base unit of a period
  abstract UnitPeriod : UnitPeriod with get
  /// Length of the period as a number of UnitPeriods
  abstract Length : int with get
  /// Start of the period as UTC DateTime
  abstract Start : DateTime with get



#nowarn "9" // no overlap of fields here
/// Optimized implementation of IPeriod interface
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
  override x.ToString() = x.value.ToString() // TODO pretty

  static member op_Explicit(value:int64) : Period =  Period(value)
  static member op_Explicit(timePeriod:Period) : int64  = timePeriod.value

  member this.Start with get(): DateTimeOffset = periodStart this.value
  member this.End with get() : DateTimeOffset = periodEnd this.value
  member this.TimeSpan with get() : TimeSpan = timeSpan this.value
  member this.Next with get() : Period = Period(addPeriods 1L this.value)
  member this.Previous with get() : Period = Period(addPeriods -1L this.value)
  member this.IsTick with get() : bool = isTick this.value
  /// This - other
  member this.Diff(other:Period) = intDiff this.value other.value
  member this.Add(diff:int64) = Period(addPeriods diff this.value)

  static member op_Explicit(timePeriod:Period) : DateTimeOffset = timePeriod.Start
  static member op_Explicit(timePeriod:Period) : DateTime = timePeriod.Start.DateTime


  static member (-) (period1 : Period, period2 : Period) : int64 = intDiff period1.value period2.value 
  static member (+) (period : Period, diff : int64) : Period = Period(addPeriods diff period.value)
  static member (+) (diff : int64, period : Period) : Period = Period(addPeriods diff period.value)

  static member Hash(tp:Period) : Period = Period(bucketHash (tp.value) (unitPeriod (tp.value)))

  interface IPeriod with
    member x.CompareTo (y:IPeriod) = 
      match y with
      | :? Period as y -> compare x.value y.value
      | _ -> invalidArg "other" "Cannot compare values of different types"
    member x.CompareTo (other:obj) = 
      match other with
      | :? Period as y -> compare x.value y.value
      | _ -> invalidArg "other" "Cannot compare values of different types"
    member x.UnitPeriod: UnitPeriod = unitPeriod x.value
    member x.Length: int = int <| length x.value
    member x.Start: DateTime = (periodStart x.value).UtcDateTime


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



