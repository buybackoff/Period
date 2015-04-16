Period
==========

Store any time period as 64-bit integer. Optimized for speed, space, compression and sorted storage like B+ trees.
The most convenient structure for financial and economic time series data, period.


Bit layout
==========

When used as a point of time (tick) it is the same as .NET's DateTime UTC ticks (100ns precision).

A time period is encoded as unit period (e.g. hour), number of unit periods in the period and the start of 
the period with 1ms precision.

![Slide 2](https://raw.githubusercontent.com/buybackoff/Period/master/BitLayout.png)

[pdf file](https://raw.githubusercontent.com/buybackoff/Period/master/BitLayout.pdf)


Rationale
=========

63 bits are enough to store any practical time period in UTC. For time series, time zone info is a part of series definition. Storing TZ info in each
time/period value is redundant (`DateTimeOffset` takes 12 bytes, 4 for offset). .NET/NodaTime allow easy convertion betwenn historical zoned time and UTC,
but this only needed for data cleaning before data is persisted or for data presentation after data is processed. For analysis, all time should be in UTC.

Ticks precision is usually not needed, some bits could be used for period. Period info is needed in addition to start/end, e.g. to distinguish between 
an hour and a minute started at the same tick. One could store data with different frequencies in separate tables, but in general the number of possible
frequencies is not fixed and custom periods are possible. With ordered key-value storage (B+-trees, Cassandra, ESENT, LMDB, and event SQL with clustered 
primary key/covering indexes) we could store data with Period keys very efficiently: all frequencies are phisically placed together and range
 queries are very fast. We could use a single table for all frequencies without big performance loss.

Bit layout of Period is optimized for compression. Difference between two adjacent period in representable as `1:Int32`, so any number of sequential Periods
could be represented as the first period and the number of periods. In addition, with bit-shuffling libraries like Blosc diffing becomes less necessary
and lexicographic bit layout helps to achieve very high compression.

TODOs
==========

* `this.ToString()` and `Period.Parse()` methods for textual representation, e.g. 12M:20150331 should be parsed as 12 months ended on the date,
201501:Q is a quarter started on 20150101, 2015Q2 is the second calendar quarter of 2015 and a shortcut (without a colon) for the long format. This is 
WIP and I could not find a concise universal format that I like.
* Integration with NodaTime (should make it optional unless Windows is proven to fail on history within relevant date ranges, since 2000s)
* Account for bitemporal data. Current thoughts are that observation is a struct with properties of time when an observation was made and at least 
status, e.g. actual or estimate (weather/earning forecasts vs. measurement/reporting). We need quite precise observation times, so won;t be able to put 
much more than ticks/millis to int64
* C# version with implicit conversions. Then base dll on C# version and add NuGet package with a single file source.
* Fix typos and language in this readme.


Install & Usage
================

For a dll use Period package:

    PM> Install-Package Period

For a single F# file that will be added to the root of your project, use Period.FSharp package:

	PM> Install-Package Period.FSharp

