namespace EgdeDancer
{
    public class TInputThread
    {
        private static bool terminated;
        private static bool newInputAvailable;
        private Thread t;
        private static string newInput;

        public TInputThread()
        {
            terminated = false;
            newInputAvailable = false;
            newInput = "";

            t = new Thread(new ThreadStart(ThreadExecute));
            t.Start();
        }

        ~TInputThread()
        {
            terminated = true;
            t.Join();
        }

        private static void ThreadExecute()
        {
            string s;
            while (!terminated)
            {
                while (!terminated && newInputAvailable) Thread.Sleep(10);
                s = Console.ReadLine();
                if (s == null) s = "quit";
                terminated = s == "quit";
                if (s != "")
                {
                    newInput = s;
                    newInputAvailable = true;
                }
            }
        }

        public bool isNewInputAvailable() { return newInputAvailable; }

        public string getNewInput()
        {
            if (!newInputAvailable) return "";
            newInputAvailable = false;
            string result = newInput;
            return result;
        }

    }
}
