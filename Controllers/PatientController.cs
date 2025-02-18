using ASPBookProject.Data;
using ASPBookProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


public class PatientViewModel
{
    public Patient? Patient { get; set; }
    public List<Antecedent>? Antecedents { get; set; }
    public List<Allergie>? Allergies { get; set; }

    public List<int> SelectedAntecedentIds { get; set; } = new List<int>();
    public List<int> SelectedAllergieIds { get; set; } = new List<int>();
}

namespace ASPBookProject.Controllers
{
    [Authorize]
    public class PatientController : Controller
    {

        private readonly ApplicationDbContext _context;

        public PatientController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public ActionResult Index(string searchString)
        {
            var patients = from p in _context.Patients select p;
            if (!string.IsNullOrEmpty(searchString))
            {
                patients = patients.Where(p => p.Nom_p.Contains(searchString) || p.Prenom_p.Contains(searchString)
                || p.Sexe_p.Contains(searchString));
                return View(patients);
            }
            return View(patients);
        }


        public async Task<IActionResult> Edit(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.Antecedents)
                .Include(p => p.Allergies)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null)
            {
                return NotFound();
            }

            var viewModel = new PatientViewModel
            {
                Patient = patient,
                Antecedents = await _context.Antecedents.ToListAsync(),
                Allergies = await _context.Allergies.ToListAsync(),
                SelectedAntecedentIds = patient.Antecedents.Select(a => a.AntecedentId).ToList() ?? new List<int>(),
                SelectedAllergieIds = patient.Allergies.Select(a => a.AllergieId).ToList() ?? new List<int>()
            };

            return View(viewModel);
        }
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PatientViewModel viewModel)
        {

            if (ModelState.IsValid)
            {
                try
                {
                    var patient = await _context.Patients
                        .Include(p => p.Antecedents)
                        .Include(p => p.Allergies)
                        .FirstOrDefaultAsync(p => p.PatientId == viewModel.Patient!.PatientId);

                    if (patient == null)
                    {
                        return NotFound();
                    }

                    patient.Nom_p = viewModel.Patient.Nom_p;
                    patient.Prenom_p = viewModel.Patient.Prenom_p;
                    patient.Sexe_p = viewModel.Patient.Sexe_p;
                    patient.Num_secu = viewModel.Patient.Num_secu;

                    patient.Allergies.Clear();
                    if (viewModel.SelectedAllergieIds != null)
                    {
                        var selectedAllergies = await _context.Allergies
                            .Where(a => viewModel.SelectedAllergieIds.Contains(a.AllergieId))
                            .ToListAsync();
                        foreach (var allergie in selectedAllergies)
                        {
                            patient.Allergies.Add(allergie);
                        }
                    }

                    patient.Antecedents.Clear();
                    if (viewModel.SelectedAntecedentIds != null)
                    {
                        var selectedAntecedents = await _context.Antecedents
                            .Where(a => viewModel.SelectedAntecedentIds.Contains(a.AntecedentId))
                            .ToListAsync();
                        foreach (var antecedent in selectedAntecedents)
                        {
                            patient.Antecedents.Add(antecedent);
                        }
                    }
                    _context.Entry(patient).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PatientExists(viewModel.Patient.PatientId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            viewModel.Antecedents = await _context.Antecedents.ToListAsync();
            viewModel.Allergies = await _context.Allergies.ToListAsync();
            return View(viewModel);
        }

        private bool PatientExists(int id)
        {
            return _context.Patients.Any(e => e.PatientId == id);
        }

        public async Task<IActionResult> ShowDetails(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.Antecedents)
                .Include(p => p.Allergies)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null)
            {
                return NotFound();
            }

            var viewModel = new PatientViewModel
            {
                Patient = patient,
                Antecedents = patient.Antecedents.ToList(),
                Allergies = patient.Allergies.ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            Patient? pati = _context.Patients.FirstOrDefault<Patient>(ins => ins.PatientId == id);

            if (pati == null)
            {
                return NotFound();
            }
            return View(pati);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int PatientId)
        {
            Patient? pati = _context.Patients.FirstOrDefault<Patient>(pat => pat.PatientId == PatientId);

            if (pati != null)
            {
                _context.Patients.Remove(pati);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var viewModel = new PatientViewModel
            {
                Allergies = await _context.Allergies.ToListAsync(),
                Antecedents = await _context.Antecedents.ToListAsync(),
                SelectedAllergieIds = new List<int>(),
                SelectedAntecedentIds = new List<int>()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Add(PatientViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                viewModel.Allergies = await _context.Allergies.ToListAsync();
                viewModel.Antecedents = await _context.Antecedents.ToListAsync();
                return View(viewModel);
            }

            Patient patient = new Patient
            {
                Nom_p = viewModel.Patient.Nom_p,
                Prenom_p = viewModel.Patient.Prenom_p,
                Sexe_p = viewModel.Patient.Sexe_p,
                Num_secu = viewModel.Patient.Num_secu,
                Allergies = new List<Allergie>(),
                Antecedents = new List<Antecedent>()
            };

            if (viewModel.SelectedAllergieIds != null)
            {
                var selectedAllergies = await _context.Allergies
                    .Where(a => viewModel.SelectedAllergieIds.Contains(a.AllergieId))
                    .ToListAsync();
                foreach (var allergie in selectedAllergies)
                {
                    patient.Allergies.Add(allergie);
                }
            }
            if (viewModel.SelectedAntecedentIds != null)
            {
                var selectedAntecedents = await _context.Antecedents
                    .Where(a => viewModel.SelectedAntecedentIds.Contains(a.AntecedentId))
                    .ToListAsync();
                foreach (var antecedent in selectedAntecedents)
                {
                    patient.Antecedents.Add(antecedent);
                }
            }
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));

        }

        
    }
}