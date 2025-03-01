using EngineLayer.Util;
using NUnit.Framework;
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
        public void Dequeue_IsNull_WhenQueueIsEmpty()
        {
            var pq = new PriorityQueue<string>(2);
            Assert.That(pq.Dequeue(), Is.Null);
        }

        [Test]
        public void Peek_IsNull_WhenQueueIsEmpty()
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
    }
}
