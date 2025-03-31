using System;
using System.Collections.Generic;
using System.Text;

namespace Universe.SqlTrace
{
    public partial class SqlTraceReader
    {
        class PlansBySpid
        {
            public int Spid;
            public List<string> SqlPlans;
        }

        class PlansBySpidBuffer
        {
            private List<PlansBySpid> Buffer = new List<PlansBySpid>();

            public PlansBySpid GetPlansBySpid(int spid)
            {
                foreach (var plan in Buffer)
                    if (plan.Spid == spid) return plan;

                return null;
            }

            public void ClearPlansForSpid(int spid)
            {
                var index = FindIndexBySpid(spid);
                if (index >= 0)
                    Buffer.RemoveAt(index);
            }


            public void AddPlanForSpid(int spid, string plan)
            {
                var index = FindIndexBySpid(spid);
                if (index < 0)
                {
                    PlansBySpid newPlans = new PlansBySpid() { Spid = spid, SqlPlans = new List<string>() { plan}};
                    Buffer.Add(newPlans);
                }
                else
                {
                    // Buffer[index].SqlPlans ??= new List<string>();
                    Buffer[index].SqlPlans.Add(plan);
                }
            }

            private int FindIndexBySpid(int spid)
            {
                int index = -1, p = 0;
                foreach (var errorBySpid in Buffer)
                {
                    if (errorBySpid.Spid == spid)
                    {
                        index = p;
                        break;
                    }

                    p++; //wha?
                }

                return index;
            }
        }
    }
}
