```

BenchmarkDotNet v0.13.12, macOS 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 2.40GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.416
  [Host]     : .NET 8.0.22 (8.0.2225.52707), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 8.0.22 (8.0.2225.52707), Arm64 RyuJIT AdvSIMD


```
| Method     | Mean     | Error     | StdDev    | Gen0      | Gen1   | Allocated |
|----------- |---------:|----------:|----------:|----------:|-------:|----------:|
| Write      | 5.054 ms | 0.0821 ms | 0.0878 ms | 4968.7500 | 7.8125 |  29.75 MB |
| WriteAsync | 4.940 ms | 0.0397 ms | 0.0331 ms | 4968.7500 | 7.8125 |  29.75 MB |
