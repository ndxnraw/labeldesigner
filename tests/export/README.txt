Voliteľné nezávislé overenie exportov po spustení scripts/test.cmd.
Vyžaduje Python (Pillow, numpy), Node.js s modulom sharp a pdftoppm (Poppler) v PATH.
Tieto nástroje nie sú závislosťami aplikácie.

python verify-exports.py "../../build/sdk-test-results/net10.0-windows/ČAS_TESTU/RotationTests"
python verify-word-parts.py "../../build/sdk-test-results/net10.0-windows/ČAS_TESTU/RotationTests"

Príkazy spustite z tohto priečinka. NODE_BINARY môže určiť cestu k node.exe;
NODE_PATH môže ukazovať na priečinok obsahujúci sharp.
PDF/SVG a PNG alternatívy: >99 % pokrytie v tolerancii jednej bunky pri rasterizácii.
ZPL: presná zhoda bitov pri zvolenom DPI.
