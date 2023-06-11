namespace EgdeDancer
{
    public class TTimeCtrl
    {
        TLimits limits;
        EColor stm;
        uint timechecks;

        long lastPlyStart;
        long softLimit;     // when this is reached we don't start another ply
        long hardLimit;     // when this is reached we stop no matter what to prevent over stepping
        int rootMoveNo;

        public void Init(TLimits aLimits, EColor aStm)
        {
            limits = aLimits;
            stm = aStm;

            timechecks = 0;

            int xTime = limits.time[(int)stm];
            int xInc = limits.inc[(int)stm];

            bool searchForMate = limits.mate > 0;

            if (limits.movestogo == 0)                          // 0 means time is for the whole game  
            {
                limits.movestogo = limits.ponder != 0 ? 40 : 50;
            }

            lastPlyStart = limits.starttime;

            if (limits.Use_Time_Management())
            {
                /***************************************************************************************************
				 * different time controls are supported
				 * Option 1: There is a time increment with each move
				 *			 softLimit    = time increment (or left time if less)
				 *							if we have more than 3 sec left, we add a small part of it to the time
				 *			 hardLimitMax = left time - 50 
				 *           hardLimit    = min (hardLimitMax, softLimit + 5 * (left time - softLimit) / moves2go)
				 ***************************************************************************************************/
                if (xInc > 0)
                {
                    // first we calculate the durations for soft and hard limit
                    long hardLimitMax;
                    if (xTime >= 200) hardLimitMax = xTime - 100; else hardLimitMax = xTime / 2;

                    softLimit = Math.Min(xInc, xTime);
                    if (xTime - softLimit > 750) softLimit += (xTime - softLimit) / limits.movestogo;

                    hardLimit = Math.Min(hardLimitMax, softLimit + 5 * (xTime - softLimit) / limits.movestogo);
                }

                /***************************************************************************************************
				 * Option 2: There is no time increment with each move
				 *			 softLimit    = time left / moves2go
				 *			 hardLimitMax = time left - (softLimit * moves2go) / 2 (we try to leave half of the time) 
				 *           hardLimit    = min(hardLimitMax, 5 * softLimit)
				 ***************************************************************************************************/
                if (xInc == 0)
                {
                    long hardLimitMax;

                    softLimit = xTime / (limits.movestogo + 1);
                    hardLimitMax = limits.movestogo > 9 ? 5 * softLimit : xTime / limits.movestogo;

                    hardLimit = Math.Min(xTime - 32 * limits.movestogo, hardLimitMax);
                }

                // transform them into timepoints
                softLimit += limits.starttime;
                hardLimit += limits.starttime;

                if (hardLimit < softLimit) softLimit = hardLimit;

                /******************************************************************************
					* Introduce a safety buffer when we are at the last move before time control)
					******************************************************************************/
                if (limits.movestogo == 1)
                {
                    if (xTime >= 500) hardLimit = xTime - 100;
                    else
                    if (xTime >= 100) hardLimit = xTime - 50;
                    else
                        hardLimit = xTime - 10;
                    softLimit = hardLimit;
                }
            }
            else
            {
                softLimit = limits.starttime + limits.movetime; // soft and hard limit are the start time + given time
                hardLimit = softLimit;
            }

            /***************************************************************
			 * Adjust limits for timer resolution
			 * our process might not wake up early enough so we might spent 
			 * time without knowing, assume we are some ms late already
			 ***************************************************************/
            hardLimit -= 10;
            if (hardLimit <= 0) hardLimit = 1;
            softLimit = Math.Min(softLimit, hardLimit);
        }


        /*********************************************************************************
		 * check whether we have enough time left to start a search of another ply
		 * heuristics show that in 80% of searches the 1st move requires more than 35%
		 * of the time required for the last full ply
		 *
		 * so we check whether this 35% of last ply search time is still left and if it
		 * is, we start another ply. This leaves an error rate of 20% where we would have
		 * finished the 1st move when we had tried it, but saves otherwise wasted time
		 * in the remaining 80% of the cases
		 *********************************************************************************/
        public bool SearchAnotherPly()
        {
            long currentTime = TLimits.Now();
            long timeNeededForLastPly, timeToSoftLimit;
            const double RATIO = 0.30;


            if (!limits.Use_Time_Management() || limits.ponder != 0) return true;    // if we are not time bound we can always start another ply

            timeNeededForLastPly = currentTime - lastPlyStart;
            lastPlyStart = currentTime;
            timeToSoftLimit = softLimit - currentTime;

            // if the time left for this move is smaller than RATIO% of the time we used for the last ply we don't start another ply
            if (timeToSoftLimit <= timeNeededForLastPly * RATIO) return false;

            return true;
        }

        // if the engine requests more time we allow an expansion once
        // if we still have a bit of time and 
        // the time was not given as fixed movetime
        void NeedMoreTime()
        {
            if (limits.Use_Time_Management() && limits.movestogo >= 3) softLimit = hardLimit;
        }
        // the opponent played the expected move
        // now the time is running for us and we are timeSensitive now
        public void Respond2ponderHit()
        {
            limits.ponder = 0;
        }

        // return whether the time is up for this move and we have to stop
        public bool TimeUp()
        {
            timechecks++;

            long currentTime = TLimits.Now();
            long ms_between_tc = (currentTime - limits.starttime) / timechecks;           // timer resolution

            if (limits.Use_Time_Management() && limits.ponder == 0)
            {
                if (currentTime >= hardLimit - ms_between_tc) return true;                      // hard stop at the hard limit
                if (currentTime >= softLimit - ms_between_tc && rootMoveNo == 0) return true;   // soft stop when still at first root move 
            }

            if (limits.movetime != 0 && currentTime >= hardLimit) return true;

            return false;
        }
        public int GetMaxDepth()
        {
            return limits.depth > 0 ? limits.depth : 60;
        }
        public void TellRootMoveNo(int aRootMoveNo)
        {
            rootMoveNo = aRootMoveNo;
        }

        /************************************************************
		* return how many nodes are between TC
		* if the engine controls the clock and is not on the last
		* move we are more flexible if the TC is long
		* in very short TC we look more often
		************************************************************/
        public int GetNodesBetweenTC()
        {
            if (limits.movetime <= 0 && limits.movestogo > 1 && softLimit >= TLimits.Now() + 1000) return 100000;
            return 10000;
        }
    }


}
