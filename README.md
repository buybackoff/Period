Period
==========

Store any time period as 64-bit integer. Optimized for speed, space, compression and sorted storage like B+ trees.
The most convenient structure for financial and economic time series data, period.


Bit layout
==========

When used as a point of time (tick) it is the same as .NET's DateTime UTC ticks (100ns precision).

A time period is encoded as unit period (e.g. hour), number of unit periods in the period and the start of 
the period with 1ms precision. Non-intraday period could store observation date

TODO link to pdf and short description why this 


Install & Usage
================

For a dll use Period package:

    PM> Install-Package Period

For a single F# file that will be added to the root of your project, use Period.FSharp package:

	PM> Install-Package Period.FSharp

