# S17 playtest timing

Target first-time completion: 20–25 minutes. The allocation below totals 23 minutes and is a proposed target. Columns with measurements are in seconds.

| Chapter | Target budget (min) | Automated active | Automated modal | Automated waiting | First-time measured |
| --- | ---: | ---: | ---: | ---: | --- |
| Arrival | 3 | 1.020 | 0.000 | 0.000 | Pending |
| Safe power | 3 | 0.356 | 0.000 | 0.000 | Pending |
| ECHO evidence | 5 | 0.409 | 0.604 | 0.000 | Pending |
| Sampling | 3 | 2.059 | 0.000 | 0.000 | Pending |
| Analysis | 3 | — | — | — | Pending |
| Report | 4 | — | — | — | Pending |
| Ending | 2 | — | — | — | Pending |
| First-time total | 23 | — | — | — | Pending |

Measured in a Unity 6000.6.9f1 Windows x64 station preview on the [W12 integration base](https://github.com/2dshligang/last-known-state-the-legacy/pull/33) with S17 measurements active. The automated driver used real station interaction commands and known correct answers, injected a 0.6-second archive reading interval, and submitted interaction rays from valid approaches without walking between sites. It completed through sampling in 4.448 session seconds. A zero in the waiting column means the route never marked a blocking wait. Analysis, report and ending were outside this measured route. These values validate the measurement path. The 20–25 minute target requires separate human observations.

For the first-time run, use a clean profile and mute audio. Record active, modal and marked waiting time for each chapter, hindrances and the number of report retries. Compare the elapsed total and per-chapter results against the budgets. Use the full playable route before assessing release pacing.
