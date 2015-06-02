using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using System.Reflection;
using System.Diagnostics;
using Gee.External.Capstone;
using System.Text.RegularExpressions;

namespace findmagic
{
    public class findmagic
    {
        public static int Main(string[] args)
        {
            Options options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                Console.WriteLine(options.GetUsage());
                return 1;
            }

            if (!Regex.IsMatch(options.Mode, "^(match|analyze)$"))
            {
                Console.WriteLine("Unknown Mode. Mode must match ^(match|analyze)$ - Exiting...");
                return 1;
            }


            List<Subroutine> subs = JsonConvert.DeserializeObject<List<Subroutine>>(File.ReadAllText(options.SubroutineDefinitionsPath));

            Analyze(options, ref subs);

            if (options.Mode == "match")
            {
                DefinitionFile constdefs = JsonConvert.DeserializeObject<DefinitionFile>(File.ReadAllText(options.ConstantsFile));

                Match(options, subs, constdefs);
            }

            Console.ReadLine();

            return 0;
        }

        private static void Match(Options options, List<Subroutine> analyze, DefinitionFile definitions)
        {
            Trace.WriteLine("Match");
            StringBuilder b = new StringBuilder();

            string log = "Rebuilding Graph...";
            b.AppendLine(log);
            Console.WriteLine(log);

            List<Subroutine> allsubs = definitions.Subroutines;
            allsubs.AddRange(definitions.AmbigousSubroutines);
            allsubs.AddRange(definitions.Unmatchable);

            foreach (Subroutine sx in definitions.Subroutines)
            {
                foreach (string s in sx.CalleesNames)
                {
                    if(!sx.Callees.Any(x => x.Name == s))
                    try
                    {
                        Subroutine callee = allsubs.First(g => g.Name == s);
                        callee.Callers.Add(sx);
                        sx.Callees.Add(callee);
                    }
                    catch (Exception)
                    {
                        string err = String.Format("Warning: Could not find sub for xref to {0} - is your definition file broken?", s);
                        Console.WriteLine(err);
                        b.AppendLine(err);
                    }
                }
            }

            foreach (Subroutine sx in definitions.AmbigousSubroutines)
            {
                foreach (string s in sx.CalleesNames)
                {
                    if (!sx.Callees.Any(x => x.Name == s))
                    try
                    {
                        Subroutine callee = allsubs.First(g => g.Name == s);
                        callee.Callers.Add(sx);
                        sx.Callees.Add(callee);
                    }
                    catch (Exception)
                    {
                        string err = String.Format("Warning: Could not find sub for xref to {0} - is your definition file broken?", s);
                        Console.WriteLine(err);
                        b.AppendLine(err);
                    }
                }
            }
            Console.WriteLine("Matching...");
            foreach (Subroutine sub in definitions.Subroutines)
            {
                if (definitions.Unmatchable.Any(y => y.Name == sub.Name) || definitions.AmbigousSubroutines.Any(y => y.Name == sub.Name)) continue;
                //Console.WriteLine("Searching for {0}...", sub.Name);
                //b.AppendFormat("Searching for {0}...\n", sub.Name);

                Subroutine msub = sub.CloneGraph();
                foreach (Subroutine s in analyze)
                {
                    if (msub.Name == "_int_malloc" && s.Name == "sub_40F760") Debugger.Break();
                    if (s != sub) continue;
                    if (!s.Name.StartsWith("sub")) continue;
                    Subroutine g = s.CloneGraph();
                    Dictionary<Subroutine, Subroutine> matching = new Dictionary<Subroutine, Subroutine>();
                    
                    if (MatchSub(msub, g, msub, g, ref matching))
                    {
                        //string slog = String.Format("Match found! {0} at 0x{1:x8}", sub.Name, s.StartAddress);
                        //Console.WriteLine(slog);
                        //b.AppendLine(slog);
                        try
                        {
                            s.AssignNamesFromGraph(sub);
                        }
                        catch(Exception)
                        { }
                    }
                }
            }
            List<long> NonExactFindings = new List<long>();
            foreach (Subroutine sub in definitions.AmbigousSubroutines)
            {
                if (definitions.Unmatchable.Any(y => y.Name == sub.Name)) continue;

                List<Subroutine> matches = new List<Subroutine>();
                Subroutine msub = sub.CloneGraph();
                foreach (Subroutine s in analyze)
                {
                    if (msub.Name == "_int_malloc" && s.Name == "sub_40F760") Debugger.Break();
                    if (s != sub) continue;
                    if (!s.Name.StartsWith("sub")) continue;
                    Subroutine g = s.CloneGraph();
                    Dictionary<Subroutine, Subroutine> matching = new Dictionary<Subroutine, Subroutine>();

                    if (MatchSub(msub, g, msub, g, ref matching))
                    {
                        matches.Add(s);
                    }
                }

                if(matches.Count == 1)
                {
                    try
                    {
                        matches[0].AssignNamesFromGraph(sub);
                    }
                    catch (Exception)
                    { }
                }
                else if(matches.Count > 1)
                {
                    string slog = String.Format("Possible match: {0} at", sub.Name);
                    foreach(Subroutine match in matches)
                    {
                        slog += String.Format(" 0x{0:x8}", match.StartAddress);
                        NonExactFindings.Add(match.StartAddress);
                    }
                    Console.WriteLine(slog);
                    b.AppendLine(slog);
                }
            }
            Console.WriteLine("Note: While these Routines may be not unique identifable, they often do similar things.\nUse them as a guideline for manual reversing.");
            b.AppendLine("Note: While these Routines may be not unique identifable, they often do similar things.\nUse them as a guideline for manual reversing.");


            Console.WriteLine("Matching finished. Printing recovered names:");
            b.AppendLine("Matching finished. Printing recorvered names:");

            foreach(Subroutine s in analyze)
            {
                if (NonExactFindings.Contains(s.StartAddress) || Regex.IsMatch(s.Name, "^sub_[0-9a-fA-F]+$")) continue;
                string slog = String.Format("{0} at 0x{1:x8}", s.Name, s.StartAddress);
                Console.WriteLine(slog);
                b.AppendLine(slog);
            }

            File.AppendAllText(options.OutputFilePath, b.ToString());
        }

        private static bool MatchSub(Subroutine g1, Subroutine g2, Subroutine c1, Subroutine c2, ref Dictionary<Subroutine, Subroutine> s)
        {
            Trace.WriteLine("MatchSub");
            int found = 0;

            foreach (Subroutine tsub in c2.GetAllNodes())
            {
                foreach (Subroutine vsub in s.Values)
                {
                    if (tsub.Name == vsub.Name)
                    {
                        found++;
                    }
                }
            }
            if (found == c2.GetAllNodes().Count) return true;

            List<KeyValuePair<Subroutine, Subroutine>> candidates = new List<KeyValuePair<Subroutine, Subroutine>>();

            if (s.Count == 0)
            {
                candidates.Add(new KeyValuePair<Subroutine, Subroutine>(c1, c2));
            }
            else
            {
                foreach (Subroutine n in c1.Callees)
                {
                    if (s.ContainsKey(n)) continue;
                    foreach (Subroutine m in c2.Callees)
                    {
                        if (s.ContainsValue(m)) continue;
                        candidates.Add(new KeyValuePair<Subroutine, Subroutine>(n, m));

                    }
                }

                if (candidates.Count == 0)
                {
                    foreach (Subroutine n in c1.Callers)
                    {
                        if (s.ContainsKey(n)) continue;
                        foreach (Subroutine m in c2.Callers)
                        {
                            if (s.ContainsValue(m)) continue;
                            candidates.Add(new KeyValuePair<Subroutine, Subroutine>(n, m));
                        }
                    }
                }
            }
            foreach (var p in candidates)
            {
                // compute the feasibilty rules
                bool r_pred = true, r_succ = true, r_in = true, r_out = true, r_new = true;

                foreach (Subroutine n in p.Key.Callers)
                {
                    if (s.ContainsKey(n))
                    {
                        if (!p.Value.Callers.Contains(s[n]))
                        {
                            r_pred = false;
                            goto check;
                        }
                    }
                }
                foreach (Subroutine n in p.Value.Callers)
                {
                    if (s.ContainsValue(n))
                    {
                        if (!p.Key.Callers.Contains(s.Where(g => g.Value == n).First().Key))
                        {
                            r_pred = false;
                            goto check;
                        }
                    }
                }

                foreach (Subroutine n in p.Key.Callees)
                {
                    if (s.ContainsKey(n))
                    {
                        if (!p.Value.Callees.Contains(s[n]))
                        {
                            r_succ = false;
                            goto check;
                        }
                    }
                }

                foreach (Subroutine n in p.Value.Callees)
                {
                    if (s.ContainsValue(n))
                    {
                        if (!p.Key.Callees.Contains(s.Where(g => g.Value == n).First().Key))
                        {
                            r_succ = false;
                            goto check;
                        }
                    }
                }

                int helper1 = 0, helper2 = 0, helper3 = 0, helper4 = 0;

                foreach (Subroutine n in p.Key.Callees)
                {
                    if (c1.Callers.Contains(n) && !s.ContainsKey(n))
                        helper1++;
                }
                foreach (Subroutine m in p.Value.Callees)
                {
                    if (c2.Callers.Contains(m) && !s.ContainsValue(m))
                        helper2++;
                }
                foreach (Subroutine n in p.Key.Callers)
                {
                    if (c1.Callers.Contains(n) && !s.ContainsKey(n))
                    {
                        helper3++;
                    }
                }
                foreach (Subroutine m in p.Value.Callers)
                {
                    if (c2.Callers.Contains(m) && !s.ContainsValue(m))
                    {
                        helper4++;
                    }
                }

                r_in = (helper1 == helper2) && (helper3 == helper4);

                helper1 = 0; helper2 = 0; helper3 = 0; helper4 = 0;

                foreach (Subroutine n in p.Key.Callees)
                {
                    if (c1.Callees.Contains(n) && !s.ContainsKey(n))
                        helper1++;
                }
                foreach (Subroutine m in p.Value.Callees)
                {
                    if (c2.Callees.Contains(m) && !s.ContainsValue(m))
                        helper2++;
                }
                foreach (Subroutine n in p.Key.Callers)
                {
                    if (c1.Callees.Contains(n) && !s.ContainsKey(n))
                    {
                        helper3++;
                    }
                }
                foreach (Subroutine m in p.Value.Callers)
                {
                    if (c2.Callees.Contains(m) && !s.ContainsValue(m))
                    {
                        helper4++;
                    }
                }

                r_out = (helper1 == helper2) && (helper3 == helper4);

                helper1 = 0; helper2 = 0; helper3 = 0; helper4 = 0;

                foreach (Subroutine n in g1.GetAllNodes())
                {
                    if (!s.ContainsKey(n) && !c1.Callers.Contains(n) && !c1.Callees.Contains(n) && p.Key.Callers.Contains(n))
                        helper1++;
                }
                foreach (Subroutine m in g2.GetAllNodes())
                {
                    if (!s.ContainsKey(m) && !c1.Callers.Contains(m) && !c1.Callees.Contains(m) && p.Key.Callers.Contains(m))
                        helper2++;
                }
                foreach (Subroutine n in g1.GetAllNodes())
                {
                    if (!s.ContainsKey(n) && !c1.Callers.Contains(n) && !c1.Callees.Contains(n) && p.Key.Callees.Contains(n))
                        helper3++;
                }
                foreach (Subroutine m in g2.GetAllNodes())
                {
                    if (!s.ContainsKey(m) && !c1.Callers.Contains(m) && !c1.Callees.Contains(m) && p.Key.Callees.Contains(m))
                        helper4++;
                }

            check:
                if (r_pred && r_succ && r_out && r_new && (p.Key == p.Value))
                {
                    s.Add(p.Key, p.Value);
                    if (MatchSub(g1, g2, p.Key, p.Value, ref s))
                    {
                        return true;
                    }
                }
            }
            foreach (var p in candidates)
            {
                if (s.ContainsKey(p.Key))
                {
                    s.Remove(p.Key);
                }
            }
            return false;
        }

        private static void Analyze(Options options, ref List<Subroutine> subs)
        {
            List<ElfSection> elfsections = new List<ElfSection>();
            using (ELF<long> bin = ELFReader.Load<long>(options.InputElfPath))
            {
                foreach (Section<long> s in bin.Sections)
                {
                    long FileOffset = 0;

                    // why can't I have an API for this. WHY
                    // actually I have an API for this now, however it isn't on nuget yet
                    foreach (PropertyInfo property in typeof(Section<long>).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        if (property.Name == "Header")
                        {
                            foreach (PropertyInfo p in property.GetValue(s).GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                            {
                                if (p.Name == "Offset")
                                {
                                    FileOffset = (long)p.GetValue(property.GetValue(s));
                                }
                            }
                        }
                    }

                    elfsections.Add(new ElfSection() { VirtualAddress = s.LoadAddress, FileAddress = FileOffset, Size = s.Size, Name = s.Name });
                }
            }

            FileStream fs = File.Open(options.InputElfPath, FileMode.Open);

            byte[] assembly = new byte[fs.Length];
            // we're limiting us here to about 2 billion bytes, or 2.147.483.647 bytes to be exact.
            // While this doesn't work for 4k ultra definition movies with 133.7 sound track, it should work for us, as we don't have 2gb assemblies
            fs.Read(assembly, 0, (int)fs.Length);



            using (var disassembler = CapstoneDisassembler.CreateX86Disassembler(DisassembleMode.Bit64))
            {
                disassembler.EnableDetails = true;
                disassembler.Syntax = DisassembleSyntaxOptionValue.Intel; // AT&T Syntax should die kthx

                StringBuilder b = new StringBuilder();

                foreach (Subroutine sub in subs)
                {
                    int sublen = (int)(sub.EndAddress - sub.StartAddress);

                    ElfSection section = elfsections.Where(x => x.VirtualAddress <= sub.StartAddress && (x.VirtualAddress + x.Size) > sub.StartAddress).First();

                    byte[] subroutine = new byte[sublen];
                    Array.ConstrainedCopy(assembly, (int)section.MapAddress(sub.StartAddress), subroutine, 0, sublen);

                    Instruction<X86Instruction, X86Register, X86InstructionGroup, X86InstructionDetail>[] instructions;
                    try
                    {
                        instructions = disassembler.DisassembleAll(subroutine, sub.StartAddress);
                    }
                    catch (InvalidOperationException)
                    {
                        Console.WriteLine("Error disassembling {0}, skipping...", sub.Name);
                        b.AppendFormat("Error disassembling {0}, skipping...\n", sub.Name);
                        continue;
                    }

                    //dis.EIP = rawDataHandle.AddrOfPinnedObject();
                    //dis.EIP += (int)sub.StartAddress; // again, we're limiting us to maximum of about 2 gigabytes. Personally, I have never seen a .text section that is larger than 100 megabytes in size, so this should be good enough in praxis
                    long fileaddress = sub.StartAddress;
                    Console.WriteLine("\r\nScanning {0}...", sub.Name);
                    b.AppendFormat("\r\nScanning {0}...\r\n", sub.Name);
                    //long endaddr = (rawDataHandle.AddrOfPinnedObject().ToInt64() + sub.EndAddress);

                    foreach (var instruction in instructions)
                    {

                        string outs = String.Empty;
                        try
                        {
                            // Some instructions will be ignored, because it is very likely that these contain constants that are useless to us.
                            // for example:
                            // test eax, 0
                            // you will see this very often, and therefore it is no good measure to distinguish functions.
                            // Usually, the good constants are in arithmetic or bitwise operations. Also included is LEA, which will be used for string xrefs.
                            // Non-Exhaustive list:
                            //     AND, OR, XOR, LEA, MOV, CALL (for xref resolution)
                            if (instruction.Id != X86Instruction.AND &&
                                instruction.Id != X86Instruction.OR &&
                                instruction.Id != X86Instruction.XOR &&
                                instruction.Id != X86Instruction.LEA &&
                                instruction.Id != X86Instruction.MOV &&
                                //instruction.Id != X86Instruction.CMP &&
                                instruction.Id != X86Instruction.CALL)
                                goto loopend;

                            if (instruction.Id == X86Instruction.CALL)
                            {
                                // only near calls are supported by now. This would break with anti-debugging-techniques that use far jumps to jump over segment boundaries,
                                // but I doubt that this affects standard libraries, like libc. Anything linked against the .a file should have all the library in the same segment
                                if (instruction.Bytes[0] != 0xE8)
                                    goto loopend;

                                sub.CalleesAdresses.Add(instruction.ArchitectureDetail.Operands[0].ImmediateValue.Value);

                                // wtf, sometimes capstone seems to precompute stuff with displacements and sometimes not ¯\_(ツ)_/¯
                                outs += String.Format("Found xref to adress 0x{0:x8}", instruction.ArchitectureDetail.Operands[0].ImmediateValue.Value);

                                goto loopend;
                            }

                            // Filter Instructions. Only immediates are interesting; in case of lea instruction we need an xref (lea xxx, qword ptr[rip + 0x1337])
                            if (!instruction.ArchitectureDetail.Operands.Any(g => g.Type == X86InstructionOperandType.Immediate)
                                && !(instruction.Id == X86Instruction.LEA && instruction.ArchitectureDetail.Operands[1].MemoryValue.BaseRegister == X86Register.RIP))
                                goto loopend;

                            if (instruction.Id == X86Instruction.LEA)
                            {
                                // compute virtual address
                                long virtxref = instruction.ArchitectureDetail.Operands[1].MemoryValue.Displacement + instruction.Address + instruction.Bytes.Length;

                                // get the corresponding elf section
                                ElfSection mySection = elfsections.Where(x => x.VirtualAddress < virtxref && virtxref < (x.VirtualAddress + x.Size)).First();
                                if (!Regex.IsMatch(mySection.Name, options.DataSectionPattern))
                                    goto loopend;
                                // and use the section to map the virtual address to a file offset to get the string
                                string foundstr = ReadStringXref(assembly, mySection.MapAddress(virtxref));

                                // filter printable characters, but allow certain non-printable stuff like newlines, carriage returns and tabs
                                if (foundstr.ToCharArray().Any(x => (x < 0x20 && x != 0x0A && x != 0x0D && x != 0x09) || x == 0x7F || x > 0xdf) || String.IsNullOrEmpty(foundstr))
                                    goto loopend;

                                // done.
                                outs = String.Format("Found string at 0x{0:x}: {1}", instruction.Address, foundstr);
                                sub.StringXrefs.Add(foundstr);
                                goto loopend;
                            }
                            else if (instruction.Id == X86Instruction.MOV)
                            {
                                if (instruction.ArchitectureDetail.Operands[1].ImmediateValue.HasValue)
                                {
                                    long addr = instruction.ArchitectureDetail.Operands[1].ImmediateValue.Value;
                                    // see if this a mappable adress
                                    ElfSection mySection = elfsections.Where(x => x.VirtualAddress < addr && addr < (x.VirtualAddress + x.Size)).FirstOrDefault();
                                    if (mySection != null && Regex.IsMatch(mySection.Name, options.DataSectionPattern))
                                    {
                                        // yes, we can map this.
                                        string foundstr = ReadStringXref(assembly, mySection.MapAddress(addr));

                                        // filter printable characters, but allow certain non-printable stuff like newlines, carriage returns and tabs
                                        if (!foundstr.ToCharArray().Any(x => (x < 0x20 && x != 0x0A && x != 0x0D && x != 0x09) || x == 0x7F || x > 0xdf) && !String.IsNullOrEmpty(foundstr))
                                        {
                                            outs = String.Format("Found string at 0x{0:x}: {1}", instruction.Address, foundstr);
                                            sub.StringXrefs.Add(foundstr);
                                            goto loopend;
                                        }
                                    }
                                }
                            }
                            // Let's see if we have an immediate that is larger than one byte
                            // this could exclude some loop checks, but this seems (as of 23.04.2015) to be a good tradeoff
                            // specifically, we do not exclude -1 (0xFFFFFFFF), because this is often used for error handling and thus comes in handy later when matching (at least I hope)
                            foreach (var operand in instruction.ArchitectureDetail.Operands.Where(x => x.ImmediateValue.HasValue))
                            {
                                long immediate = operand.ImmediateValue.Value;
                                if (immediate == 0 /*|| immediate >> 4 == 0*/)
                                    continue;

                                outs += String.Format("Found interesting constant at file offset 0x{0:x}, value = 0x{1:x}", instruction.Address, immediate);
                                sub.Constants.Add(immediate);
                            }
                        loopend:
                            ;
                        }
                        catch (Exception e)
                        {
                            outs += String.Format("\n!!! Error analyzing {0}, skipping instruction {2} {3}. ({1})", sub.Name, e.Message, instruction.Mnemonic, instruction.Operand);
                        }

                        if (!String.IsNullOrEmpty(outs))
                        {
                            b.AppendLine(outs);
                            Console.WriteLine(outs);
                        }
                    }
                }

                ElfSection plt = elfsections.Where(g => g.Name.StartsWith(".plt")).First();

                foreach (Subroutine sx in subs)
                {
                    foreach (long l in sx.CalleesAdresses)
                    {
                        if (l >= plt.VirtualAddress && l <= (plt.VirtualAddress + plt.Size))
                        {
                            // call to plt, let's ignore this
                            continue;
                        }
                        try
                        {
                            Subroutine callee = subs.First(g => g.StartAddress == l);
                            callee.Callers.Add(sx);
                            sx.AddCallee(callee);
                        }
                        catch (Exception)
                        {
                            string err = String.Format("Warning: Could not find sub for xref to {0:x8}", l);
                            Console.WriteLine(err);
                            b.AppendLine(err);
                        }
                    }
                }

                if (!String.IsNullOrWhiteSpace(options.OutputFilePath) && options.Mode == "analyze")
                {
                    Console.WriteLine("Preparing results for definition files");
                    Console.WriteLine("Sorting xrefs");

                    List<Subroutine> ambigous = new List<Subroutine>();
                    List<Subroutine> unmatchable = new List<Subroutine>();

                    for (int i = 0; i < subs.Count; i++ )
                    {
                        if ((subs[i].Constants.Count == 0 && subs[i].StringXrefs.Count == 0))
                        {
                            string sn = subs[i].Name;
                            if (!unmatchable.Any(x => x.Name == sn))
                                unmatchable.Add(subs[i]);
                        }
                    }

                    for (int i = 0; i < subs.Count - 1; i++)
                    {
                        for (int j = i + 1; j < subs.Count; j++)
                        {

                            if (subs[i].StrictEquals(subs[j]))
                            {
                                foreach (Subroutine callee in subs[i].Callees)
                                {
                                    // comparing names is enough for us, as they need to be unique anyways.
                                    if (!subs[j].Callees.Any(g => g.Name == callee.Name))
                                        goto fail;
                                }
                                foreach (Subroutine callee in subs[j].Callees)
                                {
                                    if (!subs[i].Callees.Any(g => g.Name == callee.Name))
                                        goto fail;
                                }

                                string err = String.Format("Subroutine {0} is ambigous to {1}. It will be saved to a separate definition list.", subs[i].Name, subs[j].Name);
                                Console.WriteLine(err);
                                b.AppendLine(err);

                                Subroutine t = subs[i];

                                if (!ambigous.Any(x => x.Name == t.Name))
                                    ambigous.Add(subs[i]);

                                t = subs[j];

                                if (!ambigous.Any(x => x.Name == t.Name))
                                    ambigous.Add(subs[j]);
                            fail:
                                ;

                            }
                        }
                    }
                    
                    DefinitionFile file = new DefinitionFile();


                    file.Subroutines = (from s in subs
                                        where unmatchable.Any(x => x.Name == s.Name) == false && ambigous.Any(x => x.Name == s.Name) == false
                                        select new Subroutine() { Constants = s.Constants, StringXrefs = s.StringXrefs, Name = s.Name, CalleesNames = s.CalleesNames }).ToList();
                    file.AmbigousSubroutines = (from s in ambigous
                                                where unmatchable.Any(x => x.Name == s.Name) == false
                                                select new Subroutine() { Constants = s.Constants, StringXrefs = s.StringXrefs, Name = s.Name, CalleesNames = s.CalleesNames }).ToList();
                    file.Unmatchable = (from s in unmatchable
                                        select new Subroutine() { Constants = s.Constants, StringXrefs = s.StringXrefs, Name = s.Name, CalleesNames = s.CalleesNames }).ToList();


                    File.WriteAllText(options.OutputFilePath, JsonConvert.SerializeObject(file, Formatting.Indented));

                }

                File.WriteAllText(options.OutputFilePath + ".log", b.ToString());
            }
            Console.WriteLine("Scanning finished.");
        }

        private static string ReadStringXref(byte[] buffer, long offset)
        {
            try
            {
                List<byte> list = new List<byte>();
                while (buffer[offset] != 0x00)
                {
                    list.Add(buffer[offset++]);
                }

                return Encoding.UTF8.GetString(list.ToArray());
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private static string GetR2Path()
        {
            string[] paths = Environment.GetEnvironmentVariable("path").Split(";".ToCharArray());

            string r2name = "radare2";
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                r2name += ".exe";
            }

            foreach (string path in paths)
            {
                if (File.Exists(path + r2name))
                {
                    return path + r2name;
                }
            }

            throw new FileNotFoundException("radare2 not in path");
        }
    }
}
