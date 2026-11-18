// The signed-in carry-over's refusal, for a browser gate (Fhi.Metadata-emsc0.3.16): rows seeded into the browser list,
// one of a captured variable and two of a variable the stub refuses, so «Lagre i Mine variabellister» meets Munin's
// 400 naming that variable, and the list tab draws the refusal with «Fjern disse radene» beside the offer.
import { readFileSync } from 'node:fs';

export const BROWSER_LIST_KEY = 'Fhi.Munin.Explorer:anonymous-list';

/** The variable the stub's list add refuses, as Munin refuses a row whose datasamling is not the variable's own. */
export const REFUSED_VARIABLE = 'cccccccc-0000-4000-8000-000000000016';

const captured = JSON.parse(readFileSync(
  new URL('../test/Fhi.Munin.Explorer.Tests/Testdata/variables.json', import.meta.url), 'utf8')).items[0];

// The fields the package keeps for a row (README, «What the package keeps in the reader's browser»).
const row = (variable, datasamlingId, datasamlingName) => ({
  variabelId: variable.id,
  variabelCode: variable.code,
  variabelName: variable.preferredTerm,
  kildeId: variable.kildeId,
  kildeName: variable.kildeName,
  kildeKortNavn: variable.kildeKortNavn,
  datasamlingId,
  datasamlingName,
  variabelgruppeName: variable.variabelgruppeName,
  dataType: variable.dataType,
  dataFrom: variable.dataFrom,
  dataTo: variable.dataTo,
  versjonStatus: variable.versjonStatus,
});

const refused = { ...captured, id: REFUSED_VARIABLE, code: 'V_ALS.F1.FLYTTET', preferredTerm: 'Variabel flyttet til en annen datasamling' };

/** What the state writes to the browser list's storage before it reloads the page: the package's own v:1 shape. */
export const browserList = JSON.stringify({
  v: 1,
  items: [
    row(captured, captured.datasamlingId, captured.datasamlingName),
    row(refused, 'cccccccc-0000-4000-8000-000000000161', 'Inklusjon'),
    row(refused, 'cccccccc-0000-4000-8000-000000000162', 'Oppfølging'),
  ],
});
