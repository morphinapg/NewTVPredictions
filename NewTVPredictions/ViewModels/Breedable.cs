using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Breedable
    {
        /// <summary>
        /// Random blend between two values
        /// </summary>
        /// <param name="x">first value</param>
        /// <param name="y">second value</param>
        /// <returns></returns>
        public double Breed(double x, double y)                                          
        {
            var r = Random.Shared;
            var p = r.NextDouble();

            return (x * p) + (y * (1 - p));
        }
    }
}
