namespace Zadanie_03._03;

public interface IReactorStepPrompter
{
    bool ConfirmSetupStep(string stepName);
    bool ConfirmMoveStep(
        int step,
        int total,
        string command,
        ReactorState current,
        ReactorState predicted);
}

public class ConsoleStepPrompter : IReactorStepPrompter
{
    private bool _autoApprove;

    public bool ConfirmSetupStep(string stepName)
    {
        if (_autoApprove)
            return true;

        Console.WriteLine();
        Console.WriteLine($"=== Przygotowanie: {stepName.ToUpperInvariant()} ===");
        return ReadChoice();
    }

    public bool ConfirmMoveStep(
        int step,
        int total,
        string command,
        ReactorState current,
        ReactorState predicted)
    {
        if (_autoApprove)
            return true;

        Console.WriteLine();
        Console.WriteLine(new string('─', 60));
        Console.WriteLine($"Krok {step}/{total} — proponowana komenda: {command.ToUpperInvariant()}");
        Console.WriteLine(new string('─', 60));
        PrintState("Teraz", current);
        PrintState("Po tej komendzie (symulacja)", predicted);
        Console.WriteLine();
        Console.WriteLine("[Enter] wykonaj ten krok");
        Console.WriteLine("[a]     wykonaj i kontynuuj bez dalszych pytań");
        Console.WriteLine("[q]     przerwij grę");
        return ReadChoice();
    }

    private bool ReadChoice()
    {
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input is "" or "y" or "t" or "tak")
                return true;

            if (input is "a" or "auto")
            {
                _autoApprove = true;
                Console.WriteLine("Włączono tryb auto — kolejne kroki wykonam bez pytania.");
                return true;
            }

            if (input is "q" or "quit" or "n" or "nie")
                return false;

            Console.WriteLine("Nieznana opcja. Użyj Enter, 'a' lub 'q'.");
        }
    }

    private static void PrintState(string label, ReactorState state)
    {
        Console.WriteLine($"  {label}:");
        Console.WriteLine($"    Robot: kolumna {state.PlayerCol} / {state.GoalCol}");
        Console.WriteLine($"    Rząd 5: {state.FormatBottomRow()}");
        Console.WriteLine($"    Bloki:  {state.FormatBlocks()}");
    }
}

public sealed class AutoStepPrompter : IReactorStepPrompter
{
    public bool ConfirmSetupStep(string stepName) => true;

    public bool ConfirmMoveStep(
        int step,
        int total,
        string command,
        ReactorState current,
        ReactorState predicted) => true;
}
