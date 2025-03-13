//#nullable enable
//using System.Collections.Generic;
//using System.Linq;

//namespace EngineLayer.SpectrumMatch;

//public class KeepNScoresSearchLog : TopScoringOnlySearchLog
//{
//    private readonly SortedSet<ISearchAttempt> _targetAttempts;
//    private readonly SortedSet<ISearchAttempt> _decoyAttempts;
//    private readonly uint _maxDecoysToKeep;
//    private readonly uint _maxTargetsToKeep;

//    public KeepNScoresSearchLog(double tolerance = SpectralMatch.ToleranceForScoreDifferentiation, uint maxTargetsToKeep = uint.MaxValue, uint maxDecoysToKeep = uint.MaxValue)
//        : base(tolerance)
//    {
//        _targetAttempts = new(Comparer);
//        _decoyAttempts = new SortedSet<ISearchAttempt>(Comparer);
//        _maxDecoysToKeep = maxDecoysToKeep;
//        _maxTargetsToKeep = maxTargetsToKeep;
//    }

//    public override bool Add(ISearchAttempt attempt)
//    {
//        bool added;
//        if (attempt.IsDecoy)
//        {
//            added = _decoyAttempts.Add(attempt);
//            if (_decoyAttempts.Count > _maxDecoysToKeep)
//            {
//                _decoyAttempts.Remove(_decoyAttempts.Max!);
//            }
//        }
//        else
//        {
//            added = _targetAttempts.Add(attempt);
//            if (_targetAttempts.Count > _maxTargetsToKeep)
//            {
//                _targetAttempts.Remove(_targetAttempts.Max!);
//            }
//        }

//        return added;
//    }

//    public override bool Remove(ISearchAttempt matchHypothesis)
//    {
//        bool removed = false;
//        ISearchAttempt? toRemove = matchHypothesis.IsDecoy
//            ? _decoyAttempts.FirstOrDefault(p => p is SpectralMatchHypothesis h && h.Equals(matchHypothesis))
//            : _targetAttempts.FirstOrDefault(p => p is SpectralMatchHypothesis h && h.Equals(matchHypothesis));

//        if (toRemove is null)
//            return removed;

//        removed = matchHypothesis.IsDecoy
//            ? _decoyAttempts.Remove(toRemove)
//            : _targetAttempts.Remove(toRemove);

//        // add the Minimal Search Attempt back
//        if (removed && toRemove is SpectralMatchHypothesis hyp)
//        {
//            MinimalSearchAttempt toAdd = hyp;
//            Add(toAdd);
//        }

//        return removed;
//    }

//    //public override void Clear()
//    //{
//    //    var toReplace = GetTopScoringAttemptsWithSequenceInformation().ToList();
//    //    foreach (var smHypothesis in toReplace)
//    //    {
//    //        Remove(smHypothesis);
//    //    }
//    //}

//    public override IEnumerable<ISearchAttempt> GetAttempts()
//    {
//        return _targetAttempts.Concat(_decoyAttempts);
//    }

//    public override IEnumerable<ISearchAttempt> GetAttemptsByType(bool isDecoy)
//    {
//        if (isDecoy)
//            return _decoyAttempts.AsEnumerable();
//        else
//            return _targetAttempts.AsEnumerable();
//    }

//    public override TopScoringOnlySearchLog CloneWithAttempts(IEnumerable<ISearchAttempt> attempts)
//    {
//        var toReturn = new KeepNScoresSearchLog(ToleranceForScoreDifferentiation);
//        toReturn.AddRange(attempts);
//        return toReturn;
//    }
//}