using SoftShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    internal class CalcCommand : Command
    {
        public override string Name => "Calc";

        public override string Description => "Calculates the value of a simple addition, subtraction, multiplication or division with one operator.";

        public override string GetHelpText(ICommandExecutionContext context, string subcommandName)
        {
            if (!string.IsNullOrEmpty(subcommandName)) throw new Exception($"The {this.Name} command uses no subcommands.");

            var lines = new List<string>();

            lines.Add(this.Description);
            lines.Add(string.Empty);
            lines.Add("Examples:");
            lines.Add("  Calc 2+3");
            lines.Add("  Calc 10.7 - 3.5");
            lines.Add("  Calc 2*5.02");
            lines.Add("  Calc 10 / 3");

            return string.Join(context.Output.LineTermination, lines.ToArray());
        }

        protected override Task ExecuteAsync(ICommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens)
        {
            double GetOperand(int index, int length) => Convert.ToDouble(commandLine.Substring(index, length).Trim(), CultureInfo.InvariantCulture);

            var spacePos = commandLine.IndexOf(' ');
            if (spacePos < 0) throw new Exception("Missing arguments.");

            var operatorPos = commandLine.IndexOfAny(new[] { '+', '-', '*', '/' }, spacePos);
            if (operatorPos < 0) throw new Exception("Missing operator.");

            var operand1 = GetOperand(spacePos + 1, operatorPos - spacePos - 1);
            var operand2 = GetOperand(operatorPos + 1, commandLine.Length - operatorPos - 1);

            double result = double.NaN;
            switch (commandLine[operatorPos])
            {
                case '+': result = operand1 + operand2; break;
                case '-': result = operand1 - operand2; break;
                case '*': result = operand1 * operand2; break;
                case '/': result = operand1 / operand2; break;
            }

            return context.Output.WriteLineAsync(Convert.ToString(result, CultureInfo.InvariantCulture));
        }
    }
}
