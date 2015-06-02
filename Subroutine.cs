using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace findmagic
{
    class Subroutine
    {
        public string Name { get; set; }

        public long StartAddress { get; set; }

        public long EndAddress { get; set; }

        public List<string> StringXrefs { get; set; }

        public List<long> Constants { get; set; }

        // These need to be references which isn't possible with json...
        [JsonIgnore]
        public List<Subroutine> Callers { get; set; }

        [JsonIgnore]
        public List<Subroutine> Callees { get; set; }

        [JsonIgnore]
        public List<long> CalleesAdresses { get; set; }

        public List<string> CalleesNames { get; set; }

        public Subroutine()
        {
            this.StringXrefs = new List<string>();
            this.Constants = new List<long>();
            this.CalleesAdresses = new List<long>();
            this.Callees = new List<Subroutine>();
            this.Callers = new List<Subroutine>();
            this.CalleesNames = new List<string>();
        }

        public void AddCallee(Subroutine s)
        {
            this.Callees.Add(s);
            this.CalleesNames.Add(s.Name);
        }

        public Subroutine Clone()
        {
            Subroutine s = new Subroutine()
            {
                StringXrefs = new List<string>(this.StringXrefs),
                Constants = new List<long>(this.Constants),
                CalleesNames = new List<string>(this.CalleesNames),
                CalleesAdresses = new List<long>(this.CalleesAdresses),
                StartAddress = StartAddress,
                EndAddress = EndAddress,
                Name = String.Copy(Name)
            };

            return s;
        }

        public List<Subroutine> GetAllNodes()
        {
            List<Subroutine> subs = new List<Subroutine>();
            GetAllNodesRecursive(ref subs);
            return subs;
        }

        private void GetAllNodesRecursive(ref List<Subroutine> subs)
        {
            if (!subs.Any(g => g.Name == this.Name))
            {
                subs.Add(this);
                foreach (Subroutine s in this.Callees)
                {
                    if (!subs.Any(g => g.Name == s.Name))
                    {
                        subs.Add(s);
                    }
                    s.GetAllNodesRecursive(ref subs);
                }
            }

        }

        public Subroutine CloneGraph()
        {
            Trace.WriteLine(String.Format("CloneGraph - Init ({0})", this.Name));
            List<Subroutine> sublist = new List<Subroutine>();

            Trace.WriteLine("< CloneGraph Init " + this.Name);
            return CloneGraph(ref sublist);
        }

        public Subroutine CloneGraph(ref List<Subroutine> sublist)
        {
            Trace.WriteLine("> CloneGraph");
            Subroutine t;
            if (sublist.Any(x => x.Name == this.Name))
            {
                t = sublist.First(x => x.Name == this.Name);
            }
            else
            {
                t = this.Clone();
                sublist.Add(t);
            }

            foreach (Subroutine callee in this.Callees)
            {
                Subroutine t2;
                if (sublist.Any(x => x.Name == callee.Name))
                {
                    t2 = sublist.First(x => x.Name == callee.Name);
                }
                else
                {
                    t2 = callee.CloneGraph(ref sublist);
                }

                t2.Callers.Add(t);
                t.Callees.Add(t2);



            }
            Trace.WriteLine("< CloneGraph");
            return t;
        }

        public override bool Equals(object obj)
        {
            // Strict type: Subroutines can only be compared to other subroutines
            if (!(obj is Subroutine))
                return false;

            return this.StrictEquals((Subroutine)obj);

        }

        public static bool operator ==(Subroutine a, Subroutine b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            return a.StrictEquals(b);
        }

        public static bool operator !=(Subroutine a, Subroutine b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return false;
            }

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
            {
                return true;
            }

            return !a.StrictEquals(b);
        }

        public bool Equals(Subroutine s)
        {
            // Null: Nothing is equal to NULL
            if ((object)s == null)
                return false;

            // I am equal to myself
            if (Object.ReferenceEquals(s, this))
                return true;

            // A Subroutine is considered equal to another subroutine if and only if
            // - The Number of String-xrefs is the same
            // - The Number of Constants is the same
            // - The Values of both lists match, that is for given n with n >= 0 and n smaller than the number of elements in the list,
            //   list_1[n] \element list_2 and list_2[n] \element list_1


            if (s.Constants.Count + Math.Floor((decimal)s.Constants.Count / 20) >= this.Constants.Count && s.Constants.Count <= this.Constants.Count + Math.Floor((decimal)s.Constants.Count / 20) && s.StringXrefs.Count == this.StringXrefs.Count)
            {
                // semantic feasibility rules
                foreach (string xref in s.StringXrefs)
                {
                    if (!this.StringXrefs.Contains(xref))
                        return false;
                }
                foreach (string xref in this.StringXrefs)
                {
                    if (!s.StringXrefs.Contains(xref))
                        return false;
                }
                int failcount = 0;
                int treshold = (int)Math.Floor((decimal)s.Constants.Count / 20);
                foreach (long constant in s.Constants)
                {
                    if (!this.Constants.Contains(constant))
                    {
                        if (++failcount > treshold)
                            return false;
                    }
                }
                failcount = 0;
                treshold = (int)Math.Floor((decimal)this.Constants.Count / 20);
                foreach (long constant in this.Constants)
                {
                    if (!s.Constants.Contains(constant))
                        if (++failcount > treshold)
                            return false;
                }
                return true;
            }

            return false;
        }

        internal IEnumerable<Subroutine> FindPredecessors(Subroutine c1)
        {
            List<Subroutine> ret = new List<Subroutine>();
            if (this.Callees.Contains(c1))
                ret.Add(this);
            foreach (Subroutine s in this.Callees)
            {
                if (s == c1)
                    continue;
                ret.AddRange(s.FindPredecessors(c1));
            }

            return ret;
        }

        internal void AssignNamesFromGraph(Subroutine sub)
        {
            List<Subroutine> asslist = new List<Subroutine>();
            AssignNamesFromGraphRecursive(sub, ref asslist);

        }

        private void AssignNamesFromGraphRecursive(Subroutine sub, ref List<Subroutine> assigned)
        {
            assigned.Add(this);
            this.Name = sub.Name;
            for (int i = 0; i < sub.Callees.Count; i++)
            {
                if (!assigned.Any(g => g.Name == this.Callees[i].Name))
                {
                    this.Callees[i].AssignNamesFromGraphRecursive(sub.Callees[i], ref assigned);
                }
            }
        }

        internal bool StrictEquals(Subroutine s)
        {
            // Null: Nothing is equal to NULL
            if ((object)s == null)
                return false;

            // I am equal to myself
            if (Object.ReferenceEquals(s, this))
                return true;

            // A Subroutine is considered equal to another subroutine if and only if
            // - The Number of String-xrefs is the same
            // - The Number of Constants is the same
            // - The Values of both lists match, that is for given n with n >= 0 and n smaller than the number of elements in the list,
            //   list_1[n] \element list_2 and list_2[n] \element list_1


            if (s.Constants.Count == this.Constants.Count && s.StringXrefs.Count == this.StringXrefs.Count)
            {
                // semantic feasibility rules
                foreach (string xref in s.StringXrefs)
                {
                    if (!this.StringXrefs.Contains(xref))
                        return false;
                }
                foreach (string xref in this.StringXrefs)
                {
                    if (!s.StringXrefs.Contains(xref))
                        return false;
                }

                foreach (long constant in s.Constants)
                {
                    if (!this.Constants.Contains(constant))
                    {
                        return false;
                    }
                }
                foreach (long constant in this.Constants)
                {
                    if (!s.Constants.Contains(constant))
                        return false;
                }
                return true;
            }

            return false;
        }
    }
}
