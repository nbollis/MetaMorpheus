using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqLite
{
    /// <summary>
    /// Author : Swaraj Ketan Santra
    /// Email : swaraj.ece.jgec@gmail.com
    /// Date : 25/02/2017
    /// Description : Entity/model classes as T 
    /// Use this attribute to decorate the properties on your model class.
    /// Only those properties that are having exactly the same column name of a DB table.
    /// </summary>
    public class DbColumnAttribute : Attribute
    {
        /// <summary>
        /// Set true if implicit conversion is required.
        /// </summary>
        public bool Convert { get; set; }
        /// <summary>
        /// Set true if the property is primary key in the table
        /// </summary>
        public bool IsPrimary { get; set; }
        /// <summary>
        /// Denotes if the field is an identity type or not.
        /// </summary>
        public bool IsIdentity { get; set; }
    }
}
