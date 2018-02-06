# CCSTrickleLoadTest
Load testing TVP inserts into a clustered columnstore with varying batch sizes/thread counts.

The database creation aims to remove as many bottlenecks as possible:
- Delayed durability is forced [most tests saturate the log buffer and force flushes anyway]
- Memory optimised TVP for inserts [eliminate tempdb]
- 60 minute compression delay on the columnstore [no tuple mover]

The code is designed to loop through various scenarios (varying threads/batch sizes) and log the results to console as well as to a file in c:\temp.

Some things to watch out for when you vary batch size:
- Once you insert enough rows to try and go directly into a compressed row group memory & CPU usage will skyrocket (generally not what we're aiming for)
