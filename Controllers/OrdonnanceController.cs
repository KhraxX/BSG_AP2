using System.Data.Common;
using ASPBookProject.Data;
using ASPBookProject.Models;
using ASPBookProject.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ASPBookProject.Controllers
{
    public class OrdonnanceController : Controller
    {
        private readonly UserManager<Medecin> _userManager;
        private readonly ApplicationDbContext _context;

        public OrdonnanceController(UserManager<Medecin> userManager, ApplicationDbContext context)
        {
            _context = context;
            _userManager = userManager;
        }
        [Authorize]
        public async Task<IActionResult> Index()
        {
            string? medecinId = _userManager.GetUserId(User);
            List<Ordonnance> ordonnances = new List<Ordonnance>();
            ordonnances = await _context.Ordonnances
                                .Include(o => o.Medecin)
                                .Where(o => o.MedecinId == medecinId)
                                .Include(o => o.Patient)
                                .ToListAsync();

            ordonnances.OrderByDescending(o => o.Patient.Nom_p);

            return View(ordonnances);
        }

        public async Task<IActionResult> ShowDetails(int id)
        {
            var ordonnance = await _context.Ordonnances
                .Include(o => o.Medicaments)
                .Include(o => o.Patient)
                .Include(o => o.Medecin)
                .FirstOrDefaultAsync(o => o.OrdonnanceId == id);
            if (ordonnance == null)
                return NotFound();
            var viewModel = new OrdonnanceViewModel
            {
                OrdonnanceId = ordonnance.OrdonnanceId,
                Posologie = ordonnance.Posologie,
                Date_debut = ordonnance.Date_Debut,
                Date_fin = ordonnance.Date_Fin,
                Instructions_specifique = ordonnance.Instructions_specifique,
                PatientId = ordonnance.PatientId,
                Patient = ordonnance.Patient,
                Medecin = ordonnance.Medecin,
                Medicaments = ordonnance.Medicaments.ToList(),
            };
            return View(viewModel);
        }


        public async Task<IActionResult> Add()
        {
            string? MedecinId = _userManager.GetUserId(User);
            Medecin? med = _userManager.FindByIdAsync(MedecinId).Result;
            if (med == null)
            {
                return RedirectToAction("Logout", "Account");
            }
            var viewModel = new OrdonnanceViewModel
            {
                Date_debut = DateTime.Now,
                Date_fin = DateTime.Now.AddDays(1),
                Patients = await _context.Patients.ToListAsync(),
                Medicaments = await _context.Medicaments.ToListAsync(),
                SelectedMedicamentId = new List<int>(),
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(OrdonnanceViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                viewModel.Patients = await _context.Patients.ToListAsync();
                viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                return View(viewModel);
            }
            string? MedecinId = _userManager.GetUserId(User);
            Medecin med = _userManager.FindByIdAsync(MedecinId).Result;
            int result = DateTime.Compare(viewModel.Date_debut, viewModel.Date_fin);
            if (result > 0)
            {
                ModelState.AddModelError("", "Vérifier les dates");
                viewModel.Patients = await _context.Patients.ToListAsync();
                viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                return View(viewModel);
            }
            var patient = await _context.Patients
                .Include(p => p.Antecedents)
                .Include(p => p.Allergies)
                .FirstOrDefaultAsync(p => p.PatientId == (int)viewModel.PatientId);

            if (patient == null)
                return NotFound();

            Ordonnance ordonnance = new Ordonnance
            {
                Posologie = viewModel.Posologie,
                Date_Debut = viewModel.Date_debut,
                Date_Fin = viewModel.Date_fin,
                Instructions_specifique = viewModel.Instructions_specifique,
                MedecinId = MedecinId,
                Medecin = med,
                PatientId = (int)viewModel.PatientId,
                Patient = patient,
                Medicaments = new List<Medicament>()
            };

            if (viewModel.SelectedMedicamentId != null && viewModel.SelectedMedicamentId.Count > 0)
            {
                var selectedMedicament = await _context.Medicaments
                    .Where(a => viewModel.SelectedMedicamentId.Contains(a.MedicamentId))
                    .Include(m => m.Allergies)
                    .Include(m => m.Antecedents)
                    .ToListAsync();
                foreach (var medicament in selectedMedicament)
                {
                    ordonnance.Medicaments.Add(medicament);
                }
            }
            else
            {
                viewModel.Patients = await _context.Patients.ToListAsync();
                viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                ModelState.AddModelError("", "Veuillez chosir au moins 1 médicament");
                return View(viewModel);
            }
            if (!VerifyImpossibility(ordonnance))
            {
                viewModel.Patients = await _context.Patients.ToListAsync();
                viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                ModelState.AddModelError("", "Le patient ne peux pas avoir ces médicaments");
                return View(viewModel);
            }

            await _context.Ordonnances.AddAsync(ordonnance);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var ordonnance = await _context.Ordonnances
               .Include(o => o.Medicaments).ThenInclude(m => m.Allergies)
               .Include(o => o.Medicaments).ThenInclude(m => m.Antecedents)
               .Include(o => o.Patient).ThenInclude(p => p.Allergies)
               .Include(o => o.Patient).ThenInclude(p => p.Antecedents)
               .FirstOrDefaultAsync(o => o.OrdonnanceId == id);

            if (ordonnance == null)
                return NotFound();

            var viewModel = new OrdonnanceViewModel
            {
                OrdonnanceId = ordonnance.OrdonnanceId,
                Posologie = ordonnance.Posologie,
                Date_debut = ordonnance.Date_Debut,
                Date_fin = ordonnance.Date_Fin,
                Instructions_specifique = ordonnance.Instructions_specifique,
                PatientId = ordonnance.PatientId,
                Patient = ordonnance.Patient,
                Medicaments = await _context.Medicaments.ToListAsync(),
                Patients = await _context.Patients.ToListAsync(),
                SelectedMedicamentId = ordonnance.Medicaments.Select(m => m.MedicamentId).ToList() ?? new List<int>()
            };
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrdonnanceViewModel viewModel)
        {
            if (id != viewModel.OrdonnanceId)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                viewModel.Patients = await _context.Patients.ToListAsync();
                viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                return View(viewModel);
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var ordonnance = await _context.Ordonnances
                    .Include(o => o.Medicaments).ThenInclude(m => m.Allergies)
                    .Include(o => o.Medicaments).ThenInclude(m => m.Antecedents)
                    .Include(o => o.Patient).ThenInclude(p => p.Allergies)
                    .Include(o => o.Patient).ThenInclude(p => p.Antecedents)
                    .Include(o => o.Medecin)
                    .FirstOrDefaultAsync(o => o.OrdonnanceId == id);

                    if (ordonnance == null)
                    {
                        return NotFound();
                    }

                    var patient = await _context.Patients
               .Include(p => p.Antecedents)
               .Include(p => p.Allergies)
               .FirstOrDefaultAsync(p => p.PatientId == (int)viewModel.PatientId);

                    ordonnance.Posologie = viewModel.Posologie;
                    ordonnance.Date_Debut = viewModel.Date_debut;
                    ordonnance.Date_Fin = viewModel.Date_fin;
                    ordonnance.Instructions_specifique = viewModel.Instructions_specifique;
                    ordonnance.PatientId = (int)viewModel.PatientId;
                    ordonnance.Patient = patient;


                    ordonnance.Medicaments.Clear();
                    if (viewModel.SelectedMedicamentId != null && viewModel.SelectedMedicamentId.Count > 0)
                    {
                        var selectedMedicament = await _context.Medicaments
                            .Where(a => viewModel.SelectedMedicamentId.Contains(a.MedicamentId))
                            .Include(m => m.Allergies)
                            .Include(m => m.Antecedents)
                            .ToListAsync();
                        foreach (var medicament in selectedMedicament)
                        {
                           
                            ordonnance.Medicaments.Add(medicament);
                        }

                   
                    }
                    else
                    {
                        viewModel.Patients = await _context.Patients.ToListAsync();
                        viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                        ModelState.AddModelError("", "Veuillez choisir au moins 1 médicament");
                        return View(viewModel);
                    }
                    if (!VerifyImpossibility(ordonnance))
                    {
                        viewModel.Patients = await _context.Patients.ToListAsync();
                        viewModel.Medicaments = await _context.Medicaments.ToListAsync();
                        ModelState.AddModelError("", "Le patient ne peux pas avoir ces médicaments");
                        return View(viewModel);
                    }
                    _context.Entry(ordonnance).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    ThrowException();
                    if (!OrdonnanceExist((int)viewModel.OrdonnanceId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ThrowException();
            viewModel.Patients = await _context.Patients.ToListAsync();
            viewModel.Medicaments = await _context.Medicaments.ToListAsync();
            return View(viewModel);
        }

        private bool OrdonnanceExist(int id)
        {
            return _context.Ordonnances.Any(e => e.OrdonnanceId == id);
        }


        [HttpPost]
        public IActionResult ThrowException()
        {
            throw new Exception("Une exception s'est produite, nous testons la page d'exception pour les développeurs.");
        }
        public async Task<IActionResult> Delete(int id)
        {
          var ordonnance = await _context.Ordonnances
                .Include(o => o.Medicaments)
                .Include(o => o.Patient)
                .Include(o => o.Medecin)
                .FirstOrDefaultAsync(o => o.OrdonnanceId == id);
            if (ordonnance == null)
                return NotFound();
            var viewModel = new OrdonnanceViewModel
            {
                OrdonnanceId = ordonnance.OrdonnanceId,
                Posologie = ordonnance.Posologie,
                Date_debut = ordonnance.Date_Debut,
                Date_fin = ordonnance.Date_Fin,
                Instructions_specifique = ordonnance.Instructions_specifique,
                PatientId = ordonnance.PatientId,
                Patient = ordonnance.Patient,
                Medecin = ordonnance.Medecin,
                Medicaments = ordonnance.Medicaments.ToList(),
            };
            return View(viewModel);

        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                Ordonnance? ordonnance = await _context.Ordonnances.FindAsync(id);
                if (ordonnance != null)
                {
                    _context.Ordonnances.Remove(ordonnance);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "Ordonnance");
                }
                return NotFound();
            }
            catch (DbException ex)
            {
                return RedirectToAction("Error");
            }
        }
        public bool VerifyImpossibility(Ordonnance ordonnance)
        {
            List<Allergie> allergiesMedicaments = GetAllergiesMed(ordonnance);
            List<Antecedent> antecedentsMedicaments = GetAntecedentsMed(ordonnance);
            if (ordonnance.Patient == null)
            {
                throw new ArgumentNullException(nameof(ordonnance.Patient), "Une erreur imprévu sur le patient a été trouvé.");
            }
            List<Allergie> allergiesPatient = ordonnance.Patient.Allergies;
            foreach (Allergie allergieM in allergiesMedicaments)
            {
                foreach (Allergie allergieP in allergiesPatient)
                {
                    if (allergieM == allergieP)
                    {
                        return false;
                    }
                }
            }
            List<Antecedent> antecedentsPatient = ordonnance.Patient.Antecedents;
            foreach (Antecedent antecedentM in antecedentsMedicaments)
            {
                foreach (Antecedent antecedentP in antecedentsPatient)
                {
                    if (antecedentM == antecedentP)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public List<Allergie> GetAllergiesMed(Ordonnance ordonnance)
        {
            List<Allergie> allergies = new List<Allergie>();
            if (ordonnance == null)
            {
                throw new ArgumentNullException(nameof(ordonnance), "Une erreur imprévu sur l'ordonnace a été trouvé.");
            }

            if (ordonnance.Medicaments != null)
            {
                foreach (var medicament in ordonnance.Medicaments)
                {
                    if (medicament.Allergies != null)
                    {
                        foreach (var allergie in medicament.Allergies)
                            allergies.Add(allergie);
                    }
                }


            }
            return allergies;
        }

        public List<Antecedent> GetAntecedentsMed(Ordonnance ordonnance)
        {
            List<Antecedent> antecedents = new List<Antecedent>();
            if (ordonnance == null)
            {
                throw new ArgumentNullException(nameof(ordonnance), "Une erreur imprévu sur l'ordonnace a été trouvé.");
            }

            if (ordonnance.Medicaments != null)
            {
                foreach (var medicament in ordonnance.Medicaments)
                {
                    if (medicament.Antecedents != null)
                    {
                        foreach (var antecedent in medicament.Antecedents)
                            antecedents.Add(antecedent);
                    }
                }
            }
            return antecedents;
        }

    }
}
