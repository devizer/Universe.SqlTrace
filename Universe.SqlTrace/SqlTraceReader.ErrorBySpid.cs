using System.Collections.Generic;

namespace Universe.SqlTrace
{
    public partial class SqlTraceReader
    {
        class ErrorBySpid
        {
            // Key
            public int Spid;

            // Value
            public int Error;
            public string ErrorText;
        }

        class ErrorsBySpid
        {
            private List<ErrorBySpid> Buffer = new List<ErrorBySpid>();

            public ErrorBySpid GetErrorBySpid(int spid)
            {
                foreach (var errorBySpid in Buffer)
                {
                    if (errorBySpid.Spid == spid) return errorBySpid;
                }

                return null;
            }

            public void SetErrorBySpid(int spid, int error, string errorText)
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

                if (error == 0)
                {
                    if (index >= 0)
                        Buffer.RemoveAt(index);

                    return;
                }

                if (index >= 0)
                {
                    Buffer[index].Error = error;
                    Buffer[index].ErrorText = errorText;
                }
                else
                {
                    Buffer.Add(new ErrorBySpid() { Spid = spid, Error = error, ErrorText = errorText });
                }
            }
        }
    }
}
