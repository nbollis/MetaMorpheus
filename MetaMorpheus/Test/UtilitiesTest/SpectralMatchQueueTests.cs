using EngineLayer.Util;
using NUnit.Framework;
using Omics.Fragmentation;
using Omics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EngineLayer;
using Proteomics.ProteolyticDigestion;
using EngineLayer.SpectrumMatch;

namespace Test.UtilitiesTest
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class SpectralMatchQueueTests
    {
        public class SpectralMatchQueue : PriorityQueue<(int Notch, IBioPolymerWithSetMods, List<MatchedFragmentIon> MatchedIons)>
        {
            public SpectralMatchQueue(int maxCapacity = int.MaxValue, IComparer<(int Notch, IBioPolymerWithSetMods, List<MatchedFragmentIon> MatchedIons)>? comparer = null)
                : base(maxCapacity, comparer ??= new BioPolymerNotchFragmentIonComparer())
            {
            }
        }

            [Test]
        public void Enqueue_ShouldAddItem()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void Enqueue_ShouldRemoveLowestPriorityItem_WhenAtMaxCapacity()
        {
            var pq = new SpectralMatchQueue(2);
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(3, (3, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Dequeue(), Is.EqualTo((3, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
            Assert.That(pq.Dequeue(), Is.EqualTo((2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
            Assert.That(pq.Count, Is.EqualTo(0));
        }

        [Test]
        public void Dequeue_ShouldReturnHighestPriorityItem()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            var item = pq.Dequeue();
            Assert.That(item, Is.EqualTo((2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void DequeueWithPriority_ShouldReturnHighestPriorityItem()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            var item = pq.DequeueWithPriority();
            Assert.That(item, Is.EqualTo((2, (2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>()))));
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void Peek_ShouldReturnHighestPriorityItemWithoutRemovingIt()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            var item = pq.Peek();
            Assert.That(item, Is.EqualTo((2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
            Assert.That(pq.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetEnumerator_ShouldEnumerateItemsInPriorityOrder()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(3, (3, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            var items = new List<(int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)>(pq);
            Assert.That(items,
                Is.EqualTo(new List<(int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)>
                {
                    (3, null, new List<MatchedFragmentIon>()), 
                    (2, null, new List<MatchedFragmentIon>()),
                    (1, null, new List<MatchedFragmentIon>())
                }));
        }

        [Test]
        public void Dequeue_ShouldReturnDefault_WhenQueueIsEmpty()
        {
            var pq = new SpectralMatchQueue();
            Assert.That(pq.DequeueWithPriority(), Is.Default);
            Assert.That(pq.Dequeue(), Is.Default);
        }

        [Test]
        public void Peek_ShouldReturnDefault_WhenQueueIsEmpty()
        {
            var pq = new SpectralMatchQueue();
            Assert.That(pq.Peek(), Is.Default);
        }

        [Test]
        public void Enqueue_ShouldHandleDuplicatePriorities()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (2, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>())); // identical to first, not added
            Assert.That(pq.Count, Is.EqualTo(2));

            var items = pq.ToArray();
            Assert.That(items, 
                Is.EqualTo(new (int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)[] 
                {
                    (1, null, new List<MatchedFragmentIon>()), 
                    (2, null, new List<MatchedFragmentIon>())
                }));
        }

        [Test]
        public void Enumerator_ShouldEnumerateItemsInPriorityOrder_WithDuplicatePriorities()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (1, null, new List<MatchedFragmentIon>()));
            var items = new List<(int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)>();

            while (pq.Count > 0)
            {
                var val = pq.Dequeue();
                items.Add(val);
            }

            Assert.That(items,
                Is.EqualTo(new List<(int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)>
                {
                    (1, null, new List<MatchedFragmentIon>()), 
                    (2, null, new List<MatchedFragmentIon>()),
                    (1, null, new List<MatchedFragmentIon>())
                }));
        }

        [Test]
        public void DequeWithPriorities_ShouldEnumerateItemsInPriorityOrder_WithDuplicatePriorities()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (1, null, new List<MatchedFragmentIon>()));
            var items = new List<(double, (int, IBioPolymerWithSetMods, List<MatchedFragmentIon>))?>();

            while (pq.Count > 0)
            {
                var val = pq.DequeueWithPriority();
                items.Add(val);
            }

            Assert.That(items,
                Is.EqualTo(new List<(double, (int, IBioPolymerWithSetMods, List<MatchedFragmentIon>))>
                {
                    (2, (1, null, new List<MatchedFragmentIon>())),
                    (2, (2, null, new List<MatchedFragmentIon>())),
                    (1, (1, null, new List<MatchedFragmentIon>()))
                }));
        }

        [Test]
        public void Constructor_ShouldSetMaxCapacity()
        {
            var pq = new SpectralMatchQueue(5);
            Assert.That(pq.Count, Is.EqualTo(0));

            // ensure the queue is at max capacity
            for (int i = 0; i < 10; i++)
            {
                pq.Enqueue(i, (i, null, new List<MatchedFragmentIon>()));
            }
            Assert.That(pq.Count, Is.EqualTo(5));

            // ensure the intended items are in the queue
            var items = pq.ToList();
            Assert.That(items,
                Is.EqualTo(new List<(int, IBioPolymerWithSetMods, List<MatchedFragmentIon>)>
                {
                    (9, null, new List<MatchedFragmentIon>()), (8, null, new List<MatchedFragmentIon>()),
                    (7, null, new List<MatchedFragmentIon>()), (6, null, new List<MatchedFragmentIon>()),
                    (5, null, new List<MatchedFragmentIon>())
                }));
        }

        [Test]
        public void Constructor_ShouldUseDefaultComparer()
        {
            var pq = new SpectralMatchQueue(5);
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Dequeue(), Is.EqualTo((2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void Constructor_ShouldUseCustomComparer()
        {
            var customComparer = new BioPolymerNotchFragmentIonComparer();
            var pq = new SpectralMatchQueue(5, customComparer);
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (2, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Dequeue(), Is.EqualTo((2, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void Enqueue_ShouldHandleNullValues()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(2, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo((1, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void Enqueue_ShouldHandleExtremePriorityValues()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(double.MaxValue, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(double.MinValue, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo((1, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void Enqueue_ShouldHandleNegativePriorities()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(-1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(-4, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo((1, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void Enqueue_ShouldNotHandleDuplicateItems()
        {
            var pq = new SpectralMatchQueue();
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void Enqueue_ShouldHandleCustomComparerEdgeCases()
        {
            // here our comparer does not compare Item2.
            // This means things with the same priority will appear to be the same thing and not get added a second time

            var customComparer = Comparer<(int notch, IBioPolymerWithSetMods bpwsm, List<MatchedFragmentIon> MatchedIons)>
                .Create((x, y) => y.Item1.CompareTo(x.Item1));
            var pq = new SpectralMatchQueue(3, customComparer);
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            pq.Enqueue(1, (1, null, new List<MatchedFragmentIon>()));
            Assert.That(pq.Count, Is.EqualTo(1));
            Assert.That(pq.Peek(), Is.EqualTo((1, (IBioPolymerWithSetMods)null, new List<MatchedFragmentIon>())));
        }

        [Test]
        public void TestEnqueueAndDequeue()
        {
            var comparer = new BioPolymerNotchFragmentIonComparer();
            var priorityQueue = new SpectralMatchQueue(comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            var dequeuedPsm = priorityQueue.Dequeue();

            Assert.That(dequeuedPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestPeek()
        {
            var comparer = new BioPolymerNotchFragmentIonComparer();
            var priorityQueue = new SpectralMatchQueue(comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            var peekedPsm = priorityQueue.Peek();

            Assert.That(peekedPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestEnqueueBeyondCapacity()
        {
            var comparer = new BioPolymerNotchFragmentIonComparer();
            var priorityQueue = new SpectralMatchQueue(maxCapacity: 1, comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            Assert.That(priorityQueue.Count, Is.EqualTo(1));
            var remainingPsm = priorityQueue.Dequeue();
            Assert.That(remainingPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestEnumerator()
        {
            var comparer = new BioPolymerNotchFragmentIonComparer();
            var priorityQueue = new SpectralMatchQueue(comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));
            var psm3 = (Score: 7.0, (notch: 1, bpwsm: null as IBioPolymerWithSetMods, MatchedIons: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);
            priorityQueue.Enqueue(psm3.Score, psm3.Item2);

            var items = new List<(int notch, IBioPolymerWithSetMods bpwsm, List<MatchedFragmentIon> MatchedIons)>(priorityQueue);
            Assert.That(items, Is.EqualTo(new List<(int notch, IBioPolymerWithSetMods bpwsm, List<MatchedFragmentIon> MatchedIons)> { psm2.Item2, psm3.Item2, psm1.Item2 }));
        }

        [Test]
        public void TestConstructorWithDefaultComparer()
        {
            var priorityQueue = new SpectralMatchQueue(5);
            Assert.That(priorityQueue.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestConstructorWithCustomComparer()
        {
            var customComparer = Comparer<(int notch, IBioPolymerWithSetMods bpwsm, List<MatchedFragmentIon> MatchedIons)>
                .Create((x, y) => y.Item1.CompareTo(x.Item1)); 
            var priorityQueue = new SpectralMatchQueue(5, customComparer);
            Assert.That(priorityQueue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Remove_ShouldRemoveSpecifiedItem()
        {
            var pq = new SpectralMatchQueue();
            var pwsm1 = new PeptideWithSetModifications("PEPTIDE", GlobalVariables.AllModsKnownDictionary);
            var pwsm2 = new PeptideWithSetModifications("PE[UniProt:4-carboxyglutamate on E]PTIDE", GlobalVariables.AllModsKnownDictionary);
            var psm1 = (Score: 10.0, (notch: 1, pwsm1, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm2, ions: new List<MatchedFragmentIon>()));
            var psm3 = (Score: 7.0, (notch: 2, pwsm2, ions: new List<MatchedFragmentIon>()));
            
            pq.Enqueue(psm1.Score, psm1.Item2);
            pq.Enqueue(psm2.Score, psm2.Item2);
            pq.Enqueue(psm3.Score, psm3.Item2);
            pq.Remove((1, pwsm1, []));

            Assert.That(pq.Count, Is.EqualTo(2));

            var best = pq.Peek();
            Assert.That(best, Is.EqualTo(psm3.Item2));
        }
    }
}
