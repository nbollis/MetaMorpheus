using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Database;
using Database.Sample;
using NUnit.Framework;

namespace Test.SQLiteDatabaseTests
{
    [TestFixture]
    internal class ATest
    {
        [Test]
        public void SampleDbTest()
        {
            using (var dataContext = new SampleDBContext())
            {
                dataContext.Categories.Add(new Category() { CategoryName = "Clothing" });
                dataContext.Categories.Add(new Category() { CategoryName = "Footwear" });
                dataContext.Categories.Add(new Category() { CategoryName = "Accessories" });
                dataContext.SaveChanges();

                foreach (var cat in dataContext.Categories.ToList())
                {
                    Console.WriteLine($"CategoryId= {cat.CategoryID}, CategoryName = {cat.CategoryName}");
                }
            }
        }
    }
}
