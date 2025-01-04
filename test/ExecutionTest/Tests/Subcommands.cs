using ExecutionTest.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Tests
{
    public class Subcommands : TestBase
    {
        [Fact]
        public void TestSubcmdAndNonSubcmd()
        {
            TestCommandLineOk("SubcmdAndNonSubcmd -requiredvaloption=abc",
                              "abc",
                              typeof(SubcmdAndNonSubcmdCommand));

            TestCommandLineOk("SubcmdAndNonSubcmd subcmd xyz",
                              "xyz",
                              typeof(SubcmdAndNonSubcmdCommand));

            TestCommandLineException("SubcmdAndNonSubcmd subcmdx",
                                     "SubcmdAndNonSubcmd: Unknown subcommand 'subcmdx'. Possible subcommands:  (none):   subcmd: Subcommand description.");
        }

        [Fact]
        public void TestSubcmdOnly()
        {
            // No subcommand

            TestCommandLineException("SubcmdOnly");

            // subcmd1

            TestCommandLineOk("SubcmdOnly subcmd1 xyz",
                              "xyz",
                              typeof(SubcmdOnlyCommand));

            TestCommandLineOk("SubcmdOnly subcmd1 xyz -valueoption=abc",
                              "xyzabc",
                              typeof(SubcmdOnlyCommand));

            TestCommandLineOk("SubcmdOnly subcmd1 -valueoption=abc xyz",
                              "xyzabc",
                              typeof(SubcmdOnlyCommand));

            TestCommandLineException("SubcmdOnly subcmd1",
                                     "SubcmdOnly: Missing 1 required subcmd1 argument.");

            TestCommandLineException("SubcmdOnly subcmd1 -valueoption=abc",
                                     "SubcmdOnly: Missing 1 required subcmd1 argument.");

            // subcmd2

            TestCommandLineOk("SubcmdOnly subcmd2 -requiredvaloption=abc",
                              "abc",
                              typeof(SubcmdOnlyCommand));

            TestCommandLineOk("SubcmdOnly subcmd2 xyz -requiredvaloption=abc",
                              "xyzabc",
                              typeof(SubcmdOnlyCommand));

            TestCommandLineOk("SubcmdOnly subcmd2 -requiredvaloption=abc xyz -flagoption",
                              "xyzabcflagoption",
                              typeof(SubcmdOnlyCommand));

            // Unknown subcommand

            TestCommandLineException("SubcmdOnly subcmd3",
                                     "SubcmdOnly: Unknown subcommand 'subcmd3'. Possible subcommands:  subcmd1: Subcommand 1 description.  subcmd2: Subcommand 2 description.");
        }
    }
}
