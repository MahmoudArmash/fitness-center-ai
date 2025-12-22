namespace FitnessCenter.Models
{
    public class TrainerExpertise
    {
        public int TrainerId { get; set; }
        public Trainer Trainer { get; set; } = null!;

        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;
    }
}

