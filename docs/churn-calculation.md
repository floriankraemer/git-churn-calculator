# How churn is calculated

This document describes how **Git Churn Calculator** derives file metrics and the **Churn Risk Score**. For running the tool, see the [README](../README.md).

The CLI reports several git-derived metrics plus a **Churn Risk Score**. Only tracked files with at least one commit in history are included (`TotalCommits > 0`).

## Inputs from git

- **TotalCommits** — how many times the file appears across `git log` (each commit that lists the file counts once).
- **LinesAdded** / **LinesRemoved** — cumulative line insertions and deletions summed from `git log --numstat` over the **same history window** as **TotalCommits** (full history, or commits on or before `AsOf` in time-series mode). Binary file rows (`-`/`-`) contribute zero to both totals. Paths are gathered with `core.quotepath=false` so keys match tracked paths.
- **FirstCommitDate** / **LastCommitDate** — timestamps from the log for that file’s first and latest appearance.
- **TotalUniqueAuthors** — count of distinct author emails that touched the file over all time (other author columns use the same idea over rolling windows).

## Change frequency (velocity)

Let **now** be the analysis time (UTC), **first** the file’s first commit date, and:

```math
\text{AgeDays} = \max\bigl(1,\ \text{whole days from first to now}\bigr)
```

Average commit rates use fixed day-length denominators (same as the implementation):

```math
\text{ChangesPerWeek} = \frac{\text{TotalCommits}}{\text{AgeDays} / 7}
\qquad
\text{ChangesPerMonth} = \frac{\text{TotalCommits}}{\text{AgeDays} / 30.44}
\qquad
\text{ChangesPerYear} = \frac{\text{TotalCommits}}{\text{AgeDays} / 365.25}
```

These values are rounded for display (two decimal places in output). The risk score uses the unrounded `ChangesPerWeek` internally, then the final score is rounded to four decimals.

## Churn Risk Score

Let $c = \text{ChangesPerWeek}$, $A = \text{TotalUniqueAuthors}$, and $p$ be line coverage in percent ($0 \le p \le 100$) when Cobertura data exists for that file.

**Without coverage** (no `--coverage` file): coverage is not applied and the risk multiplier is always $1$:

```math
\text{ChurnRiskScore} = c \times A
```

**With coverage** (`--coverage` and a mapped Cobertura class for that path):

```math
\text{ChurnRiskScore} = c \times A \times \left(1 - \frac{p}{100}\right)
```

The factor $\left(1 - \frac{p}{100}\right)$ is **higher when coverage is lower** (no coverage → multiplier $1$; full coverage → multiplier $0$, so the score is $0$ aside from rounding). If a Cobertura file is supplied but a git file is **not** matched to any class, it is treated as **$p = 0$** (same multiplier as “no tests” in the formula above).

| Symbol / field | Meaning |
|----------------|---------|
| $c$ (**ChangesPerWeek**) | Commit velocity over the file’s lifetime |
| $A$ (**TotalUniqueAuthors**) | How many different people have touched the file |
| $1 - p/100$ | Test-gap multiplier from line coverage (only when `--coverage` is used and the file maps into the report) |

## Time series mode

When `--series week` or `--series month` is used together with `--from` (and optionally `--to`), the tool runs the full analysis repeatedly — once per bucket end date — over the specified date range.

### How "as of" anchoring works

Each analysis point receives an **`AsOf`** date (the bucket end). The tool passes this date as `--until` to every `git log` query, so history is bounded: commits after `AsOf` are invisible to that point's analysis. This means:

- **`TotalCommits`**, **`LinesAdded`**, **`LinesRemoved`**, **`FirstCommitDate`**, **`LastCommitDate`**, and **`TotalUniqueAuthors`** only reflect history on or before `AsOf`.
- Rolling windows (`CommitsLast7Days`, `UniqueAuthorsLast30Days`, etc.) are calculated relative to `AsOf`, not the wall-clock time the tool is run.
- **`AgeDays`** is the span from a file's first commit (as of that date) to `AsOf`.

### Output shape

| Format | Shape |
|--------|-------|
| JSON | Array of `{ "asOf": "yyyy-MM-dd", "files": [...] }` objects, one per time point |
| CSV | Flat rows with a leading `AsOf` column; one row per `(asOf, file)` pair |
| HTML | Bootstrap page with one collapsible `<details>` section per time point |

### Performance

The tool executes **one full set of git queries per time point**. For a weekly series covering one year that is approximately 52 × 11 = 572 `git log` invocations (including one `--numstat` pass per bucket for line totals). On large repositories or long date ranges this can take several minutes.

## Interpreting the Churn Risk Score

There is no universal threshold that separates "good" from "bad" — the right numbers depend heavily on your team size, release cadence, codebase maturity, and what you are trying to find. The guidance below describes how the score behaves and offers starting points you can adjust.

### What the score captures

A **high score** means a file is changed frequently by many different people and (when coverage data is used) is poorly tested. A **low score** means the file is stable, has few authors, or is well-covered — or some combination of the three.

The score is **unbounded above zero**. A file touched once per week by a single author with no tests scores `1.0`; a file touched ten times per week by five authors with 20 % coverage scores `10 × 5 × 0.80 = 40.0`.

### Relative ranking is usually more useful than absolute values

Because the score scale depends on your project's commit velocity and team size, comparing files **within the same repository and time window** is far more actionable than comparing numbers across different projects.

Practical approaches:

- **Sort descending and focus on the top N** — the files at the top of the list are your highest-priority review targets regardless of the raw number.
- **Watch the score over time** — a file whose score is climbing week-over-week deserves attention even if its absolute value is modest.
- **Use the distribution** — if 90 % of files score below 5 and a handful score above 50, those outliers are worth investigating first.

### Factors that shift the thresholds

- **Small, active teams** produce higher scores naturally because a small number of authors each contribute many commits. Normalise expectations accordingly.
- **Generated or vendor files** often have very high line-change counts but are not meaningful churn. Exclude them with path filters if they dominate the results.
- **Long-lived repositories** accumulate a high `TotalCommits` for core files — this is expected for foundational modules. Compare against files of similar age and purpose.
- **Coverage data absent** — without `--coverage`, two files with identical commit velocity and author counts will score equally even if one has 100 % tests. Add coverage data to make the score reflect test quality.
- **New files** have a short history, so `AgeDays` is small and `ChangesPerWeek` can be artificially high in the first few weeks. Consider filtering files younger than a threshold when looking for structural hotspots.

## Cobertura coverage mapping

The tool maps Cobertura XML `<class filename="...">` entries to git-tracked files using:

1. **Source prefix stripping** — uses `<source>` elements to turn absolute paths into relative ones
2. **Exact match** against git file paths
3. **Suffix match** — finds the git file whose path ends with the coverage filename
4. **Filename match** — falls back to matching just the filename portion
