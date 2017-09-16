using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net.Http;
using LittleChapel;


namespace PhotoAPI
{
    public static class API
    {
        public static List<Plan> GetPlan(int PlanID, WMS objWMS)
        {
            return objWMS.Plans.Where(x => x.PlanID == PlanID).ToList();

        }

        public static List<Customer> GetCustomer(int CustomerID, WMS objWMS)
        {
            return objWMS.Customers.Where(x => x.CustomerID == CustomerID).ToList();
        }
        
    }
}
