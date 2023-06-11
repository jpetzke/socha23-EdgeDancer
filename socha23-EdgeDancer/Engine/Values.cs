
using Weights;

namespace EgdeDancer
{
    public class TValue
    {
        public int mg;
        public int eg;

        public TValue() { mg = 0; eg = 0; }
        public void Set(int _mg, int _eg) { mg = _mg; eg = _eg; }
        public void Set(double _mg, double _eg) { mg = (int) _mg; eg = (int) _eg; }
    } 

    public class TValues
    {
        // EVALUATION ----------------------------------------------------------------------------------------------

        // score for the collected fishes
        public TValue MULTI_FISHES = new TValue();
        public TValue[] MOBILITY = new TValue[22];      // max 21 squares reachable
        public TValue[] AGILITY = new TValue[7];        // max 6 directions moveable
        public TValue[] SCOREABILITY = new TValue[41];   // fishes in move pattern capped at 40
        public TValue CAPTURED_FLOE_PCT = new TValue();   // Pct of total fish value on the floe that adds to the score
        public TValue TRAPPED_PENALTY = new TValue();
        public TValue RAZOR_MARGIN = new TValue();
        public TValue STATIC_0_MOVE_MARGIN = new TValue();
        public TValue LMR_MINIMUM_MOVES = new TValue();

        public TValues(TWeights weights) 
        { 
            for (int i=0; i<MOBILITY.Length; i++) { MOBILITY[i] = new TValue(); }
            for (int i = 0; i < AGILITY.Length; i++) { AGILITY[i] = new TValue(); }
            for (int i = 0; i < SCOREABILITY.Length; i++) { SCOREABILITY[i] = new TValue(); }

            CalculateValuesFromWeights(weights); 
        }
        public void CalculateValuesFromWeights(TWeights weights)
        {
            // material multiplier
            MULTI_FISHES.Set(weights.GetWeightMG(EWeight.WID_FISH_MATERIAL), weights.GetWeightEG(EWeight.WID_FISH_MATERIAL));

            // mobility 0..21 squares
            for (int i = 0; i < MOBILITY.Length; i++) MOBILITY[i].Set(  weights.GetWeightMG(EWeight.WID_MOBILITY) * (Math.Log(i + 2) - 2), 
                                                                        weights.GetWeightEG(EWeight.WID_MOBILITY) * (Math.Log(i + 2) - 2) );
            // agility 0..6 directions
            for (int i = 0; i < AGILITY.Length; i++) AGILITY[i].Set(    weights.GetWeightMG(EWeight.WID_AGILITY) * (Math.Log(i + 1) - 1),
                                                                        weights.GetWeightEG(EWeight.WID_AGILITY) * (Math.Log(i + 1) - 1));
            // scoreability 0..40 fishes
            for (int i = 0; i < SCOREABILITY.Length; i++) SCOREABILITY[i].Set(  weights.GetWeightMG(EWeight.WID_SCOREABILITY) * (Math.Log(i + 2) - 2),
                                                                                weights.GetWeightEG(EWeight.WID_SCOREABILITY) * (Math.Log(i + 2) - 2) );
            // pct of the total fish value on a captued floe that is added to the score
            CAPTURED_FLOE_PCT.Set(weights.GetWeightMG(EWeight.WID_CAPTURED_FLOE_PCT), weights.GetWeightEG(EWeight.WID_CAPTURED_FLOE_PCT));

            // trapped penalty, penalties have negative values
            TRAPPED_PENALTY.Set(-1 * weights.GetWeightMG(EWeight.WID_TRAPPED_PENALTY), -1 * weights.GetWeightEG(EWeight.WID_TRAPPED_PENALTY));

            // razor margin
            RAZOR_MARGIN.Set(weights.GetWeightMG(EWeight.WID_RAZOR_MARGIN), weights.GetWeightEG(EWeight.WID_RAZOR_MARGIN));

            // static 0 move margin per depth
            STATIC_0_MOVE_MARGIN.Set(weights.GetWeightMG(EWeight.WID_STATIC_0_MOVE_MARGIN), weights.GetWeightEG(EWeight.WID_STATIC_0_MOVE_MARGIN));

            // LMR minimum moves
            LMR_MINIMUM_MOVES.Set(weights.GetWeightMG(EWeight.WID_LMR_MINIMUM_MOVES), weights.GetWeightEG(EWeight.WID_LMR_MINIMUM_MOVES));
        }
    }
}