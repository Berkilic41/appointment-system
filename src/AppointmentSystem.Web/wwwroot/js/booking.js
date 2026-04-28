// Booking flow: pick service → load slots via AJAX → confirm.
(function () {
  let selectedServiceId = null;
  let selectedDuration = 30;
  let selectedSlot = null;

  const grid = document.getElementById('slot-grid');
  const status = document.getElementById('slot-status');
  const dateInput = document.getElementById('slot-date');
  const confirmForm = document.getElementById('confirm-form');
  const cfSvc = document.getElementById('cf-serviceId');
  const cfStart = document.getElementById('cf-startUtc');
  const cfSummary = document.getElementById('cf-summary');

  document.querySelectorAll('.service-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.service-btn').forEach(b => b.classList.remove('btn-primary', 'text-white'));
      document.querySelectorAll('.service-btn').forEach(b => b.classList.add('btn-outline-primary'));
      btn.classList.remove('btn-outline-primary');
      btn.classList.add('btn-primary', 'text-white');
      selectedServiceId = parseInt(btn.dataset.serviceId, 10);
      selectedDuration = parseInt(btn.dataset.duration, 10);
      loadSlots();
    });
    if (btn.dataset.selected === '1') btn.click();
  });

  dateInput.addEventListener('change', loadSlots);

  async function loadSlots() {
    if (!selectedServiceId) return;
    grid.innerHTML = '';
    status.textContent = 'Loading available slots…';
    confirmForm.style.display = 'none';
    selectedSlot = null;

    const url = `/api/availability?providerId=${window.PROVIDER_ID}&serviceId=${selectedServiceId}&date=${dateInput.value}`;
    const res = await fetch(url);
    if (!res.ok) { status.textContent = 'Failed to load slots.'; return; }
    const slots = await res.json();
    if (slots.length === 0) {
      status.textContent = 'Provider does not work on this day.';
      return;
    }
    status.textContent = slots.filter(s => s.available).length + ' slot(s) available.';
    slots.forEach(s => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'slot';
      btn.textContent = s.label;
      if (!s.available) {
        btn.disabled = true;
      } else {
        btn.addEventListener('click', () => selectSlot(s, btn));
      }
      grid.appendChild(btn);
    });
  }

  function selectSlot(slot, btn) {
    document.querySelectorAll('.slot.selected').forEach(b => b.classList.remove('selected'));
    btn.classList.add('selected');
    selectedSlot = slot;
    cfSvc.value = selectedServiceId;
    cfStart.value = slot.startUtc;
    const start = new Date(slot.startUtc);
    cfSummary.innerHTML = `Booking for <strong>${start.toLocaleString()}</strong> (${selectedDuration} min).`;
    confirmForm.style.display = '';
    confirmForm.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
})();
