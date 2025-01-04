using ExecutionTest.Commands;
using SoftShell;
using System.Reflection;

namespace ExecutionTest.Tests
{
     public class ParametersAndOptions : TestBase
     {
        readonly string[] None = { };

        [Fact]
        public void TestNoArgsOrOptions()
        {
            HasCommandParamsAndOptions(null, None, None, None, None, None);

            TestCommandLineOk("ParmsOptions",
                              string.Empty,
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions a", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions a b", "ParmsOptions: 2 too many arguments.");
            TestCommandLineException("ParmsOptions -x=0", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions -y", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestOneRequiredParameter()
        {
            HasCommandParamsAndOptions(null, new string[] {"requiredparam"}, None, None, None, None);

            TestCommandLineOk("ParmsOptions a",
                              "a",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions", "ParmsOptions: Missing 1 required argument.");
            TestCommandLineException("ParmsOptions a b", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions a b c", "ParmsOptions: 2 too many arguments.");
            TestCommandLineException("ParmsOptions a -x=0", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions -y a", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestOneOptionalParameter()
        {
            HasCommandParamsAndOptions(null, None, new string[] { "optionalparam" }, None, None, None);

            TestCommandLineOk("ParmsOptions",
                              string.Empty,
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions a",
                              "a",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions a b", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions a b c", "ParmsOptions: 2 too many arguments.");
            TestCommandLineException("ParmsOptions -x=0", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions a -x=0", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions -y a", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestRequiredAndOptionalParameter()
        {
            HasCommandParamsAndOptions(null, new string[] { "requiredparam" }, new string[] { "optionalparam" }, None, None, None);

            TestCommandLineOk("ParmsOptions a",
                              "a",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions a b",
                              "a b",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions", "ParmsOptions: Missing 1 required argument.");
            TestCommandLineException("ParmsOptions a b c", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions a b c d", "ParmsOptions: 2 too many arguments.");
            TestCommandLineException("ParmsOptions a -x=0", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions a -x=0 b", "ParmsOptions: Unexpected option '-x'.");
            TestCommandLineException("ParmsOptions -y a", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestOneRequiredOption()
        {
            HasCommandParamsAndOptions(null, None, None, new string[] { "requiredoption" }, None, None);

            TestCommandLineOk("ParmsOptions -requiredoption=0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -requiredoption =0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -requiredoption= 0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -requiredoption = 0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions", "ParmsOptions: Missing required option '-requiredoption'.");
            TestCommandLineException("ParmsOptions -requiredoption", "ParmsOptions: Option '-requiredoption' is missing a value.");
            TestCommandLineException("ParmsOptions a -requiredoption=0", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -requiredoption=0 a", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -requiredoption=0 -y=0", "ParmsOptions: Unexpected option '-y'.");
            TestCommandLineException("ParmsOptions -y -requiredoption=0", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestOneValueOption()
        {
            HasCommandParamsAndOptions(null, None, None, None, new string[] { "valoption" }, None);

            TestCommandLineOk("ParmsOptions",
                              string.Empty,
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -valoption=0",
                              "-valoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -valoption =0",
                              "-valoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -valoption= 0",
                              "-valoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -valoption = 0",
                              "-valoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions -valoption", "ParmsOptions: Option '-valoption' is missing a value.");
            TestCommandLineException("ParmsOptions a -valoption=0", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -valoption=0 a", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -valoption=0 -y=0", "ParmsOptions: Unexpected option '-y'.");
            TestCommandLineException("ParmsOptions -y -valoption=0", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestOneFlagOption()
        {
            HasCommandParamsAndOptions(null, None, None, None, None, new string[] { "flagoption" });

            TestCommandLineOk("ParmsOptions",
                              string.Empty,
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -flagoption",
                              "-flagoption=True",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions -flagoption=0", "ParmsOptions: Option '-flagoption' is not supposed to have a value.");
            TestCommandLineException("ParmsOptions a -flagoption", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -flagoption a", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -flagoption -y=0", "ParmsOptions: Unexpected option '-y'.");
            TestCommandLineException("ParmsOptions -y -flagoption", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestRequiredAndOptionalOptions()
        {
            HasCommandParamsAndOptions(null, None, None, new string[] { "requiredoption" }, new string[] { "valoption" }, new string[] { "flagoption" });

            TestCommandLineOk("ParmsOptions -requiredoption=0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -requiredoption=0 -valoption=1",
                              "-requiredoption=0 -valoption=1",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions -flagoption -requiredoption=0",
                              "-requiredoption=0 -flagoption=True",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions -requiredoption", "ParmsOptions: Option '-requiredoption' is missing a value.");
            TestCommandLineException("ParmsOptions -valoption -requiredoption=0", "ParmsOptions: Option '-valoption' is missing a value.");
            TestCommandLineException("ParmsOptions -flagoption=0 -requiredoption=0", "ParmsOptions: Option '-flagoption' is not supposed to have a value.");
            TestCommandLineException("ParmsOptions a -requiredoption=0", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -requiredoption=0 a -flagoption", "ParmsOptions: 1 too many arguments.");
            TestCommandLineException("ParmsOptions -y -requiredoption=0", "ParmsOptions: Unexpected option '-y'.");
            TestCommandLineException("ParmsOptions -requiredoption=0 -y=0 -flagoption", "ParmsOptions: Unexpected option '-y'.");
        }

        [Fact]
        public void TestSubcommandRequiredAndOptionalOptions()
        {
            HasCommandParamsAndOptions("subcmd", None, None, new string[] { "requiredoption" }, new string[] { "valoption" }, new string[] { "flagoption" });

            TestCommandLineOk("ParmsOptions subcmd -requiredoption=0",
                              "-requiredoption=0",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions subcmd -requiredoption=0 -valoption=1",
                              "-requiredoption=0 -valoption=1",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineOk("ParmsOptions subcmd -flagoption -requiredoption=0",
                              "-requiredoption=0 -flagoption=True",
                              typeof(ParmsAndOptionsCommand));

            TestCommandLineException("ParmsOptions subcmd -requiredoption", "ParmsOptions: subcmd option '-requiredoption' is missing a value.");
            TestCommandLineException("ParmsOptions subcmd -valoption -requiredoption=0", "ParmsOptions: subcmd option '-valoption' is missing a value.");
            TestCommandLineException("ParmsOptions subcmd -flagoption=0 -requiredoption=0", "ParmsOptions: subcmd option '-flagoption' is not supposed to have a value.");
            TestCommandLineException("ParmsOptions subcmd a -requiredoption=0", "ParmsOptions: 1 too many subcmd arguments.");
            TestCommandLineException("ParmsOptions subcmd -requiredoption=0 a -flagoption", "ParmsOptions: 1 too many subcmd arguments.");
            TestCommandLineException("ParmsOptions subcmd -y -requiredoption=0", "ParmsOptions: Unexpected subcmd option '-y'.");
            TestCommandLineException("ParmsOptions subcmd -requiredoption=0 -y=0 -flagoption", "ParmsOptions: Unexpected subcmd option '-y'.");
        }

        private void HasCommandParamsAndOptions(
                    string? subcommand,
                    string[] requiredParams,
                    string[] optionalParams,
                    string[] requiredValueOptions,
                    string[] optionalValueOptions,
                    string[] flagOptions)
        {
            var command = TestCommands.OfType<ParmsAndOptionsCommand>().First();

            command.HasParamsAndOptions(subcommand, requiredParams, optionalParams, requiredValueOptions, optionalValueOptions, flagOptions);
        }
    }
}