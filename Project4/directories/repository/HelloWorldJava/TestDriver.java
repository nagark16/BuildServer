package directories.build.HelloWorldJava;

public class TestDriver implements ITest{
	
	public boolean test()
    {
      TestedOne one = new TestedOne();
      one.sayOne();
      TestedTwo two = new TestedTwo();
      two.sayTwo();
      return true; 
    }

	public static void main(String[] args) {
		System.out.println("\n  TestDriver running:");
		TestDriver td = new TestDriver();
		td.test();
		System.out.println("\n\n");
	}

}
