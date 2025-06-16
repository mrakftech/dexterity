using Domain.Contracts;
using Domain.Entities.PatientManagement;

namespace Domain.Entities.Consultation.Documents;

public class PatientSketch : IBaseId
{
    public Guid Id { get; set; }
    public string Sketch { get; set; }
    public Patient Patient { get; set; }
    public Guid PatientId { get; set; }
    public Guid SketchCategoryId { get; set; }
}