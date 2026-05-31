namespace PowerPrompt.Models;

public enum RewriteOption
{
    CorrectSpelling,
    Polish,
    Professionalize
}

public static class RewriteOptionInfo
{
    public static readonly RewriteOption[] All =
    {
        RewriteOption.CorrectSpelling,
        RewriteOption.Polish,
        RewriteOption.Professionalize
    };

    public static string DisplayName(RewriteOption option) => option switch
    {
        RewriteOption.CorrectSpelling => "Correct spelling",
        RewriteOption.Polish => "Polish",
        RewriteOption.Professionalize => "Professionalize",
        _ => option.ToString()
    };
}
