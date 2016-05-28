using System.IO;
using elios.Persist;

namespace Examples
{
    class Program
    {
        private const string FileName1 = "classroom.xml";
        private const string FileName2 = "automata.xml";

        static void Main(string[] args)
        {

            var classroomSerializer = new XmlSerializer(typeof(Classroom));

            using (var writeStream = new FileStream(FileName1, FileMode.Create))
                classroomSerializer.Write(writeStream, Classroom.SampleClassroom());

            using (var readStream = new FileStream(FileName1, FileMode.Open))
            {
                Classroom newClassroom = (Classroom)classroomSerializer.Read(readStream);
            }

            var automataSerializer = new XmlSerializer(typeof (Automata), new[] {typeof (CommandTransition), typeof (ConditionTransition)});


            using (var writeStream = new FileStream(FileName2, FileMode.Create))
                automataSerializer.Write(writeStream, Automata.SampleAutomata());

            using (var readStream = new FileStream(FileName2, FileMode.Open))
            {
                Automata newAutomata = (Automata)automataSerializer.Read(readStream);
            }

        }
    }
}
