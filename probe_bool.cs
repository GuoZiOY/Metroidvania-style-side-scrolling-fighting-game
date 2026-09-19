// probe: does `classInstance == false` compile in C#?
public class Foo { public int X; }

public static class Probe
{
    public static bool Test(Foo entityStats)
    {
        if (entityStats == false)
            return false;
        return true;
    }
}
