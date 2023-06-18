namespace FenRecordParser;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Example FEN record string: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();

#pragma warning disable CS8604 // warning suppressed to ensure constructor is capable of handling null input
            BoardPosition boardPosition = new(input);
#pragma warning restore CS8604

            if (boardPosition.Succeeded())
            {
                Printer.PrintBlitboard(boardPosition.GetBlitboard());
                continue;
            }

            Console.WriteLine(boardPosition.GetErrorMessage());
        }
    }
}