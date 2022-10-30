namespace d60.Cirqus.Testing;

class TestWriter : IWriter 
{
	public TestWriter()
	{
		Buffer = "";
	}

	public string Buffer { get; private set; }

	public void WriteLine(string text)
	{
		Buffer += text + "\r\n";
		Console.WriteLine(text);
	}
}