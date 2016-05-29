using elios.Persist;

namespace Examples
{
    class Program
    {
        private const string FileName1 = "classroom.xml";
        private const string FileName2 = "automata.xml";

        static void Main(string[] args)
        {
            ArchiveUtils.Write(FileName1,Classroom.SampleClassroom());

            var newClassroom = ArchiveUtils.Read<Classroom>(FileName1);

            var xmlArchive = new XmlArchive(typeof(Automata), new[] {typeof(CommandTransition), typeof(ConditionTransition)});

            xmlArchive.Write(FileName2, Automata.SampleAutomata());
            var newAutomata = xmlArchive.Read(FileName2);
        }
    }
}
