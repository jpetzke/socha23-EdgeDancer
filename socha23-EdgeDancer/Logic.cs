using SochaClient;
using EgdeDancer;
using System.Globalization;

namespace SochaClientLogic
{
    public class Logic
    {
        public PlayerTeam MyTeam;
        public State GameState;
        TEngine engine;

        public Logic()
        {
            // TODO: Add init logic

            engine = new TEngine();
        }

        public SochaClient.Move GetMove()
        {
            // TODO: Add your game logic
            Console.WriteLine("Entering GetMove() at " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));

            string fen = GameState.GetFEN();
            Console.WriteLine("FEN: {0}",fen);
            
            engine.Position("position fen " + fen);
            TLimits limits = new TLimits();
            limits.movetime = GameState.Turn < 2 ? 1000 : 1500;
            uint move = engine.Search(ref limits);
            Console.WriteLine("bestmove: {0}", EgdeDancer.Move.SAN(move));
           
            Point from = null;
            Point to = null;

            if (!EgdeDancer.Move.IsNullMove(move)) to = new Point(EgdeDancer.Move.ToX(move), EgdeDancer.Move.ToY(move));
            if (!EgdeDancer.Move.IsNullMove(move) && !EgdeDancer.Move.IsSetMove(move)) from = new Point(EgdeDancer.Move.FromX(move), EgdeDancer.Move.FromY(move));

            SochaClient.Move mv = new SochaClient.Move(from, to);

            Console.WriteLine("Leaving GetMove() at " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            Console.WriteLine(String.Format("Time spent in GetMove() was {0} ms", TLimits.Now() - limits.starttime));

            return mv;
          
            //return GameState.GetAllPossibleMoves().First();
        }
    }
}
