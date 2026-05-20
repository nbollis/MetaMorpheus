Findings
1. Whole-test rejection based on “too many significant databases” is not statistically valid.  
   TransientDatabaseResultsManager.ComputePValuesForAllDatabases(...) removes a test when test.SignificantResults >= resultCount / 10 (TransientDatabaseResultsManager.cs:290-295). That is outcome-dependent test selection. Once you decide whether a test exists in the family by looking at its p-values, the downstream BH correction and combined p-values are no longer calibrated in the usual sense.
2. Per-database exclusion is only valid when it reflects structural non-identifiability, not low observed signal.  
   The framework encodes database-level exclusion as ShouldSkip -> NaN (StatisticalTestBase.cs:23-29). That can be defensible for cases like “K-S needs at least 2 values” (TestCollection.cs:76-81, 108-115, 121-138, 239-255). It is much less defensible when exclusion is triggered by the same evidence the test is supposed to evaluate, such as low confident counts or low de novo counts (TestCollection.cs:99-106, 141-183, 189-252). In that case you are conditioning on the response, which biases the tested population upward.
3. TestPassedRatio and combined significance are not directly comparable across databases when each database is tested on a different subset of tests.  
   StatisticalTestsRun, StatisticalTestsPassed, and TestPassedRatio are computed after NaN filtering (TransientDatabaseResultsManager.cs:349-354). A database that qualifies for only easy-to-pass tests, or only a few tests, can look better than one evaluated on a harder or larger family. That is not fatal, but it means the ratio is not a stable ranking statistic unless eligibility is standardized.
4. Several test null models are weak or mismatched to the data-generating process.
   - GaussianTest<T> fits a normal distribution to database-level values and does one-sided tail tests (GaussianTest.cs:49-103). Many metrics are bounded ratios, zero-inflated, or skewed; Gaussian tails are a poor default.
   - NegativeBinomialTest<T> claims proteome-size normalization in the class summary (NegativeBinomialTest.cs:10-14, 24-25) but actually fits raw extracted counts with no offset/exposure term (29-132). That is a model mismatch if database size is a driver.
   - PermutationTest<T> is not a classical permutation test. It redistributes counts according to TransientProteinCount weights or resamples database-level summaries (PermutationTest.cs:70-131, 138-269). That assumes exchangeability at the database-summary level, which is hard to justify biologically or statistically.
   - KolmogorovSmirnovTest pools all databases into the background, including the focal database (KolmogorovSmirnovTest.cs:52-59). That contaminates the reference distribution and weakens interpretability. It is not leave-one-out.
   - FisherExactTest is the cleanest of the group, but it still compares each database to “rest of dataset” (FisherExactTest.cs:80-108), so the per-database tests are dependent.
5. The current framework mixes screening, inference, and ranking into one pass.  
   RunStatisticalAnalysis() computes per-test p-values, drops tests, BH-corrects survivors, then combines p-values across tests (TransientDatabaseResultsManager.cs:216-257, 360-378). That makes it hard to state a clean inferential target. Are you doing formal hypothesis testing, heuristic ranking, or both? Right now it is partly all three.
---
What Is Valid About Per-Database Rejection?
Per-database rejection is valid if all of the following are true:
- The rule is pre-specified.
- The rule is based on test definability, not on whether the database “looks weak”.
- The rule is independent of extremeness under the null, or at least close to it.
- The resulting test family is interpreted as:  
  “among eligible databases for this test, which show excess evidence?”
Examples that are valid or mostly valid:
- K-S requires at least 2 observations in a sample.
- RT/distribution tests require non-empty arrays.
- A model requiring a variance estimate may need at least 2 valid observations.
Examples that are shaky:
- Skipping a count-based enrichment test because the observed count is below a threshold.
- Skipping a score/mean test because the same signal being tested is too low.
- Using one thresholding rule for some databases and then comparing combined significance across all databases.
Bottom line:  
NaN is fine if it means not statistically identifiable for this test.  
NaN is not fine if it means weak evidence, so we chose not to test it.
---
What Is Valid About Rejecting Whole Tests?
Rejecting whole tests can be valid, but only as a pre-inference design decision or a diagnostic flag, not as a post-hoc deletion based on observed significance rates.
Valid reasons to remove a test:
- The test’s assumptions are not met in this run.
- The metric is structurally missing for almost all databases.
- Simulation/null calibration shows the test is anti-conservative.
- The metric is redundant and intentionally removed before inference.
Not valid:
- “This test found too many significant databases, so it must be noisy.”  
  That is exactly the kind of post-selection that breaks family-wise interpretation.
If a test fires on 10%+ of databases, that could mean:
- The test is miscalibrated.
- The null is wrong.
- The dataset truly has broad signal.
- The metric is globally shifted.
Those possibilities need diagnosis, not deletion.
---
Validity Of The Tests Themselves
Best grounded
- FisherExactTest: reasonable for contingency-style enrichment if the table definition is biologically meaningful. Still dependent across databases because each uses “rest of dataset” as comparator.
Potentially usable with redesign
- NegativeBinomialTest: useful idea for counts, but should become a count model with an exposure term such as log(TransientProteinCount) or another opportunity denominator.
- KolmogorovSmirnovTest: useful if the reference is defined properly, ideally leave-one-out or against an external null/control.
Weak as currently implemented
- GaussianTest on ratios/medians/count-derived summaries as a general default.
- PermutationTest at the current database-summary level. The exchangeability argument is too weak.
---
A Better Theory For This System
You need to split the framework into 3 layers.
1. Eligibility
- Define when a database is eligible for a metric/test.
- This should be structural only.
- Example:
  - distribution test: n >= 2
  - RT mean test: at least 1 valid RT prediction
  - protein-group test: parsimony was run and at least 1 protein group exists
2. Inference
- For each eligible database, compute a p-value from a model with a clear null.
- Keep the test family fixed.
- Do not drop tests because of observed results.
3. Ranking / aggregation
- Build a ranking score after inference.
- This can combine:
  - number of valid tests
  - number of passed tests
  - combined p-value
  - effect sizes
- But ranking should be described as ranking, not formal hypothesis testing unless calibrated.
---
Recommended Redesign
1. Separate structural missingness from low-signal observations.
- Keep NaN only for “test undefined”.
- Do not skip count tests because the count is low; zero is a valid count.
2. Remove the >=10% significant rejection rule from inferential flow.
- Keep it only as a warning/diagnostic.
- Example: emit TestSummary.Warning = "High global hit rate; review calibration".
3. Replace generic Gaussian tests with model-specific tests.
- Counts: Poisson/NB regression with offset.
- Proportions: binomial / beta-binomial / logistic model.
- Continuous bounded summaries: permutation or bootstrap only if exchangeability is justified, otherwise rank-based or hierarchical modeling.
4. Redefine the permutation layer or remove it.
- If you want a resampling test, resample at the observation level that is actually exchangeable.
- Right now the database-summary resampling is not strong theory.
5. Make K-S leave-one-out.
- Compare database i to pooled background excluding i.
- Better yet, compare to an explicit negative-control distribution if available.
6. Track effect sizes alongside p-values and rank on both.
- Counts alone should not decide.
- Examples:
  - log fold enrichment
  - odds ratio
  - standardized RT improvement
  - fragmentation median shift
7. Calibrate empirically.
- The most important next step is null simulation.
- Run the full framework on:
  - shuffled database labels
  - synthetic null databases
  - known-negative organisms
- Measure:
  - p-value uniformity
  - false positive rate
  - test-test correlation
  - stability of rankings
---
Practical Rule Set I’d Recommend
- NaN only when the test is undefined.
- Keep all predefined tests in the family for the run.
- BH correct within each fixed test family over eligible databases only.
- Combine p-values only across tests that were predefined and valid for that database.
- Report Combined as a ranking/integration score unless calibration shows it is inferentially trustworthy.
- Remove any heuristic post-hoc test deletion from the inferential path.
---
Most Important Immediate Conclusion
The current framework is best thought of as a heuristic evidence-ranking system with p-values attached, not yet a fully defensible statistical testing framework. The key step to make it defensible is to stop doing outcome-driven filtering and replace it with predeclared eligibility plus calibrated models.
If you want, I can do the next step in one of two directions:
1. Write an internal design note / wiki page: StatisticalFramework-TheoryAndRedesign.md
2. Start refactoring the code by first removing post-hoc test rejection and formalizing eligibility rules
