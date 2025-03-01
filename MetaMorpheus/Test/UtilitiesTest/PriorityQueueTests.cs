using EngineLayer.Util;
using NUnit.Framework;
using Omics.Fragmentation;
using Omics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Test.UtilitiesTest
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class PriorityQueueTests
    {
        [Test]
        public void Enqueue_ShouldAddItem()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item1");
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void Enqueue_ShouldRemoveLowestPriorityItem_WhenAtMaxCapacity()
        {
            var pq = new PriorityQueue<string>(2);
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item2");
            pq.Enqueue(3, "item3");
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo("item3"));
        }

        [Test]
        public void Dequeue_ShouldReturnHighestPriorityItem()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item2");
            var item = pq.Dequeue();
            Assert.That(item, Is.EqualTo("item2"));
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test]
        public void Peek_ShouldReturnHighestPriorityItemWithoutRemovingIt()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item2");
            var item = pq.Peek();
            Assert.That(item, Is.EqualTo("item2"));
            Assert.That(pq.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetEnumerator_ShouldEnumerateItemsInPriorityOrder()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item1");
            pq.Enqueue(3, "item3");
            pq.Enqueue(2, "item2");
            var items = new List<string>(pq);
            Assert.That(items, Is.EqualTo(new List<string> { "item1", "item2", "item3" }));
        }

        [Test]
        public void Dequeue_ShouldReturnNull_WhenQueueIsEmpty()
        {
            var pq = new PriorityQueue<string>(2);
            Assert.That(pq.Dequeue(), Is.Null);
        }

        [Test]
        public void Peek_ShouldReturnNull_WhenQueueIsEmpty()
        {
            var pq = new PriorityQueue<string>(2);
            Assert.That(pq.Peek(), Is.Null);
        }

        [Test]
        public void Enqueue_ShouldHandleDuplicatePriorities()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item1");
            pq.Enqueue(1, "item2");
            pq.Enqueue(1, "item3");
            Assert.That(pq.Count, Is.EqualTo(3));
            var items = new List<string>(pq);
            Assert.That(items, Is.EqualTo(new List<string> { "item1", "item2", "item3" }));
        }

        [Test]
        public void Enumerator_ShouldEnumerateItemsInPriorityOrder_WithDuplicatePriorities()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(2, "item2");
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item3");
            var items = new List<string>(pq);
            Assert.That(items, Is.EqualTo(new List<string> { "item1", "item2", "item3" }));
        }

        [Test]
        public void Constructor_ShouldSetMaxCapacity()
        {
            var pq = new PriorityQueue<string>(5);
            Assert.That(pq.Count, Is.EqualTo(0));

            // ensure the queue is at max capacity
            for (int i = 0; i < 10; i++)
            {
                pq.Enqueue(i, $"item{i}");
            }
            Assert.That(pq.Count, Is.EqualTo(5));

            // ensure the intended items are in the queue
            var items = new List<string>(pq);
            Assert.That(items, Is.EqualTo(new List<string> { "item5", "item6", "item7", "item8", "item9" }));
        }

        [Test]
        public void Constructor_ShouldUseDefaultComparer()
        {
            var pq = new PriorityQueue<string>(5);
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item2");
            Assert.That(pq.Dequeue(), Is.EqualTo("item2"));
        }

        [Test]
        public void Constructor_ShouldUseCustomComparer()
        {
            var customComparer = Comparer<(double, string)>.Create((x, y) => y.Item1.CompareTo(x.Item1));
            var pq = new PriorityQueue<string>(5, customComparer);
            pq.Enqueue(1, "item1");
            pq.Enqueue(2, "item2");
            Assert.That(pq.Dequeue(), Is.EqualTo("item1"));
        }

        [Test]
        public void Enqueue_ShouldHandleNullValues()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, null);
            pq.Enqueue(1, "null");
            pq.Enqueue(1, null);
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo("null"));
        }

        [Test]
        public void Enqueue_ShouldHandleExtremePriorityValues()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(double.MaxValue, "max");
            pq.Enqueue(double.MinValue, "min");
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo("max"));
        }

        [Test]
        public void Enqueue_ShouldHandleNegativePriorities()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(-1, "negative");
            pq.Enqueue(-4, "more negative");
            Assert.That(pq.Count, Is.EqualTo(2));
            Assert.That(pq.Peek(), Is.EqualTo("negative"));
        }

        [Test]
        public void Enqueue_ShouldNotHandleDuplicateItems()
        {
            var pq = new PriorityQueue<string>(3);
            pq.Enqueue(1, "item");
            pq.Enqueue(1, "item");
            Assert.That(pq.Count, Is.EqualTo(1));
        }

        [Test] 
        public void Enqueue_ShouldHandleCustomComparerEdgeCases()
        {
            // here our comparer does not compare Item2.
            // This means things with the same priority will appear to be the same thing and not get added a second time

            var customComparer = Comparer<(double, string)>
                .Create((x, y) => y.Item1.CompareTo(x.Item1));
            var pq = new PriorityQueue<string>(3, customComparer);
            pq.Enqueue(1, "item1");
            pq.Enqueue(1, "item2");
            Assert.That(pq.Count, Is.EqualTo(1));
            Assert.That(pq.Peek(), Is.EqualTo("item1"));
        }

        [Test]
        public void TestEnqueueAndDequeue()
        {
            var comparer = new TentativePsmComparer();
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>
                (comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            var dequeuedPsm = priorityQueue.Dequeue();

            Assert.That(dequeuedPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestPeek()
        {
            var comparer = new TentativePsmComparer();
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>
                (comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            var peekedPsm = priorityQueue.Peek();

            Assert.That(peekedPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestEnqueueBeyondCapacity()
        {
            var comparer = new TentativePsmComparer();
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>
                (maxCapacity: 1, comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);

            Assert.That(priorityQueue.Count, Is.EqualTo(1));
            var remainingPsm = priorityQueue.Dequeue();
            Assert.That(remainingPsm, Is.EqualTo(psm1.Item2));
        }

        [Test]
        public void TestEnumerator()
        {
            var comparer = new TentativePsmComparer();
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>
                (comparer: comparer);

            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm3 = (Score: 7.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            priorityQueue.Enqueue(psm1.Score, psm1.Item2);
            priorityQueue.Enqueue(psm2.Score, psm2.Item2);
            priorityQueue.Enqueue(psm3.Score, psm3.Item2);

            var items = new List<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>(priorityQueue);
            Assert.That(items, Is.EqualTo(new List<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)> { psm2.Item2, psm3.Item2, psm1.Item2 }));
        }

        [Test]
        public void TestConstructorWithDefaultComparer()
        {
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>(5);
            Assert.That(priorityQueue.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestConstructorWithCustomComparer()
        {
            var customComparer = Comparer<(double, (int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions))>.Create((x, y) => y.Item1.CompareTo(x.Item1));
            var priorityQueue = new PriorityQueue<(int notch, IBioPolymerWithSetMods pwsm, List<MatchedFragmentIon> ions)>(5, customComparer);
            Assert.That(priorityQueue.Count, Is.EqualTo(0));
        }
    }
}
