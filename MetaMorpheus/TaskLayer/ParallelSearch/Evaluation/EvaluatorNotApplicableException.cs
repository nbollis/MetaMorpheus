#nullable enable
using System;

namespace TaskLayer.ParallelSearch.Evaluation;

/// <summary>
/// Thrown by an evaluator that cannot say anything about this input, such as a model that needs more databases than
/// were searched. The runner reports it as skipped, with the message as the reason, rather than as a failure.
/// </summary>
public sealed class EvaluatorNotApplicableException(string message) : Exception(message);
