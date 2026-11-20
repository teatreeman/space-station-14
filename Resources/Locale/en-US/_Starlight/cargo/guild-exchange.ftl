## The terminal window. Every time shown is station time.

guild-exchange-window-title = Guild Exchange
guild-exchange-window-brand = GUILD EXCHANGE TERMINAL
guild-exchange-window-licence = NT Commerce Office licence
guild-exchange-window-motto = Interstellar Trade Guild · In profit we trust
guild-exchange-window-command-placeholder = TICKER <GO>
guild-exchange-window-clock = {$time} STATION
guild-exchange-window-markets = MARKETS
guild-exchange-window-watch-header = TKR     BID    ASK    CHG
guild-exchange-window-company-title = {$ticker} · {$name} · {$sector}
guild-exchange-window-quote = LAST ₷{$last}  BID {$bid}  ASK {$ask}  CHG {$change}  VOL {$volume}
guild-exchange-window-range = H {$high}  L {$low}
guild-exchange-window-tf-1 = 30S
guild-exchange-window-tf-5 = 2.5M
guild-exchange-window-tf-10 = 5M
guild-exchange-window-ma = MA10
guild-exchange-window-order-entry = ORDER ENTRY
guild-exchange-window-side-buy = BUY
guild-exchange-window-side-sell = SELL
guild-exchange-window-side-short = SHORT
guild-exchange-window-side-cover = COVER
guild-exchange-window-type-market = MARKET
guild-exchange-window-type-limit = LIMIT
guild-exchange-window-qty = QTY
guild-exchange-window-limit = LIMIT ₷
guild-exchange-window-max = MAX
guild-exchange-window-submit = SEND ORDER
guild-exchange-window-estimate-cost = Costs about ₷{$money}
guild-exchange-window-estimate-proceeds = Pays about ₷{$money}
guild-exchange-window-estimate-short = Margin ₷{$margin} · collateral ₷{$money}
guild-exchange-window-bad-qty = Enter how many shares.
guild-exchange-window-portfolio = PORTFOLIO
guild-exchange-window-account-personal = Personal
guild-exchange-window-account-cargo = Cargo
guild-exchange-window-full-budget = Full budget
guild-exchange-window-full-budget-on = The whole cargo budget is unlocked.
guild-exchange-window-withdraw = Withdraw
guild-exchange-window-cash = CASH ₷{$cash}
guild-exchange-window-budget = CARGO BUDGET ₷{$cash}
guild-exchange-window-cap = CAN STILL INVEST ₷{$cap}
guild-exchange-window-deposit-hint = Use spesos on the terminal to deposit them. Your account is tied to your ID card.
guild-exchange-window-no-id = Bring an ID card to trade. Your account stays with the card.
guild-exchange-window-pnl = P/L ₷{$realised} closed · ₷{$open} open
guild-exchange-window-positions-header = TKR    QTY     AVG     P/L   P/L%
guild-exchange-window-no-positions = No positions.
guild-exchange-window-health =   margin {$health}, called under {$call}
guild-exchange-window-orders = OPEN ORDERS {$count}/{$max}
guild-exchange-window-order-buy = BUY {$shares} {$ticker} at ₷{$limit}
guild-exchange-window-order-sell = SELL {$shares} {$ticker} at ₷{$limit}
guild-exchange-window-cancel = X
guild-exchange-window-wire = GALAXY NOW
guild-exchange-window-calendar = CALENDAR
guild-exchange-window-tape = TAPE
guild-exchange-window-calendar-earnings = {$time} {$ticker} EARNINGS  est ₷{$consensus}
guild-exchange-window-calendar-reported = {$time} {$ticker} EARNINGS  ₷{$actual} vs ₷{$consensus}
guild-exchange-window-calendar-dividend = {$time} {$ticker} DIVIDEND  ₷{$amount} a share
guild-exchange-window-unconfirmed = UNCONFIRMED
guild-exchange-window-no-news = No news yet.
guild-exchange-window-no-trades = No trades yet.
guild-exchange-window-costs = Fee {$fee}% · borrowing {$borrow}% a tick · short margin {$margin}%
guild-exchange-window-help = DONK <GO> picks a company. BUY 10 <GO>, SELL ALL <GO>, SHORT 5 NT <GO> and COVER ALL <GO> send market orders.
guild-exchange-window-unknown-command = Unknown command. Try HELP <GO>.

## Why an order didn't go through.

guild-exchange-no-id = You need an ID card to trade on your own account.
guild-exchange-no-station = This terminal isn't connected to a station budget.
guild-exchange-no-access = Only cargo staff can trade the cargo budget.
guild-exchange-full-budget-denied = Only the quartermaster can unlock the whole budget.
guild-exchange-no-funds = Not enough spesos.
guild-exchange-cap = That's over cargo's quarter-budget cap. Only the quartermaster can lift it.
guild-exchange-cooldown = The Guild is still processing your last order.
guild-exchange-not-enough-shares = You don't hold that many shares.
guild-exchange-not-enough-short = You haven't shorted that many shares.
guild-exchange-cover-first = Cover your short in this company first.
guild-exchange-sell-first = Sell your shares in this company before shorting it.
guild-exchange-bad-order = That order doesn't make sense.
guild-exchange-too-many-orders = You have too many open orders.
guild-exchange-closed = The exchange is closed.
guild-exchange-deposited = Deposited {$amount} spesos.

## The trade tape.

guild-exchange-cargo-trader = Cargo
guild-exchange-log-bought = {$trader} bought {$shares} {$ticker} for ₷{$money}
guild-exchange-log-sold = {$trader} sold {$shares} {$ticker} for ₷{$money}
guild-exchange-log-shorted = {$trader} shorted {$shares} {$ticker} for ₷{$money}
guild-exchange-log-covered = {$trader} covered {$shares} {$ticker} for ₷{$money}
guild-exchange-log-margin-call = MARGIN CALL at {$time}: {$trader} covered {$shares} {$ticker} for ₷{$money}

## Round end.

guild-exchange-round-end-bell = [color=#ffb000]Guild Exchange:[/color] closing bell at {$time} station time. Every short covered, every share sold.
guild-exchange-round-end-cargo = [color=#ffb000]Guild Exchange:[/color] {$station}'s cargo portfolio closed at {$profit} spesos.
guild-exchange-round-end-best = [color=#ffb000]Guild Exchange:[/color] {$name} made the most trading on their own account: {$profit} spesos.

## Sectors and companies.

guild-exchange-sector-conglomerate = Conglomerate, plasma
guild-exchange-sector-cybernetics = Cybernetics, AI
guild-exchange-sector-food = Food, logistics
guild-exchange-sector-pharma = Pharmaceuticals
guild-exchange-sector-plasma = Plasma
guild-exchange-sector-medical = Medical response
guild-exchange-sector-mining = Mining equipment
guild-exchange-sector-weapons = Energy weapons

guild-exchange-company-nt = NanoTrasen
guild-exchange-company-nt-desc = Owns this station and most of the ones nearby. Slow to move, hard to sink.
guild-exchange-company-cybs = Cybersun Industries
guild-exchange-company-cybs-desc = NanoTrasen's loudest rival, and proud of it.
guild-exchange-company-donk = Donk Co.
guild-exchange-company-donk-desc = Cheap, everywhere, and nobody asks what's in the crates.
guild-exchange-company-intd = Interdyne Pharmaceuticals
guild-exchange-company-intd-desc = Sells the cure. Some say it sells the disease too.
guild-exchange-company-vsky = Violet Sky Systems
guild-exchange-company-vsky-desc = A young outfit with big promises. Cheap shares that swing hard.
guild-exchange-company-mtak = MedTak Interstellar
guild-exchange-company-mtak-desc = Paid per call-out, so it does best when stations do worst.
guild-exchange-company-vulc = Vulcan Industries
guild-exchange-company-vulc-desc = Every salvage crew in the sector has its logo on something.
guild-exchange-company-wafl = Waffle Co.
guild-exchange-company-wafl-desc = Sells to anyone who pays. Very volatile.

## Company news. Each company has its own good and bad lines; {$company} is its name.

guild-exchange-news-nt-good-1 = {$company} posts record plasma output for the quarter
guild-exchange-news-nt-good-2 = {$company} wins a Federation station-building contract
guild-exchange-news-nt-good-3 = {$company} raises its full-year outlook on strong refinery margins
guild-exchange-news-nt-good-4 = Board approves a {$company} share buyback worth billions
guild-exchange-news-nt-good-5 = {$company} station insurance claims fall for a third straight quarter
guild-exchange-news-nt-good-6 = {$company} buys a bankrupt shipyard for scrap prices
guild-exchange-news-nt-good-7 = Central Command signs off on six new {$company} research outposts
guild-exchange-news-nt-good-8 = Federation drops its antitrust case against {$company}
guild-exchange-news-nt-good-9 = {$company} crew retention hits a ten-year high, somehow
guild-exchange-news-nt-good-10 = {$company} plasma futures sold out through next cycle
guild-exchange-news-nt-good-11 = Analysts upgrade {$company} to "buy, obviously"
guild-exchange-news-nt-good-12 = {$company} closes its longest-running loss-making station
guild-exchange-news-nt-good-13 = {$company} signs a decade-long supply deal with the colonial fleet
guild-exchange-news-nt-good-14 = New {$company} bluespace relay cuts shipping times in half
guild-exchange-news-nt-good-15 = {$company} beats a crew wage claim in arbitration
guild-exchange-news-nt-good-16 = Pension funds pile into {$company} after a calm quarter
guild-exchange-news-nt-bad-1 = {$company} station lost to a supermatter accident in a nearby sector
guild-exchange-news-nt-bad-2 = Shareholders question {$company} safety record after a crew strike
guild-exchange-news-nt-bad-3 = {$company} cuts guidance, blames "unforeseen station events"
guild-exchange-news-nt-bad-4 = {$company} plasma shipment vanishes somewhere past the frontier
guild-exchange-news-nt-bad-5 = Federation opens an inquiry into {$company} crew contracts
guild-exchange-news-nt-bad-6 = {$company} recalls a run of emergency oxygen tanks
guild-exchange-news-nt-bad-7 = Class action over {$company} clone memory loss moves to trial
guild-exchange-news-nt-bad-8 = {$company} research station goes dark; Central Command says nothing
guild-exchange-news-nt-bad-9 = Credit agency puts {$company} on negative watch
guild-exchange-news-nt-bad-10 = {$company} loses a colonial contract to a cheaper rival
guild-exchange-news-nt-bad-11 = Leaked memo shows {$company} skipped station inspections for a year
guild-exchange-news-nt-bad-12 = {$company} security budget cut, insurers not amused
guild-exchange-news-nt-bad-13 = Dock workers walk out at three {$company} ports
guild-exchange-news-nt-bad-14 = {$company} writes off a half-built station
guild-exchange-news-nt-bad-15 = {$company} chief financial officer leaves "to spend time with family"
guild-exchange-news-nt-bad-16 = Plasma glut squeezes {$company} refinery margins

guild-exchange-news-cybs-good-1 = {$company} unveils a new line of positronic brains
guild-exchange-news-cybs-good-2 = {$company} signs a cybernetics deal with three Federation hospitals
guild-exchange-news-cybs-good-3 = {$company} implant orders double in a quarter
guild-exchange-news-cybs-good-4 = {$company} lures a NanoTrasen research chief across the aisle
guild-exchange-news-cybs-good-5 = {$company} AI passes Federation safety certification on the first try
guild-exchange-news-cybs-good-6 = Militia fleet orders {$company} targeting cores in bulk
guild-exchange-news-cybs-good-7 = {$company} prosthetics win a design prize and a waiting list
guild-exchange-news-cybs-good-8 = {$company} wins a patent fight over synthetic eyes
guild-exchange-news-cybs-good-9 = {$company} buys a borg chassis maker for cash
guild-exchange-news-cybs-good-10 = Analysts call {$company} "the only cyborg stock that matters"
guild-exchange-news-cybs-good-11 = {$company} cuts implant prices and still grows margins
guild-exchange-news-cybs-good-12 = {$company} signs a long contract to supply frontier medbays
guild-exchange-news-cybs-good-13 = {$company} opens a research campus next to a NanoTrasen station, pointedly
guild-exchange-news-cybs-good-14 = Demand for {$company} mantis blades outstrips supply
guild-exchange-news-cybs-good-15 = {$company} beta firmware fixes the twitching, users report
guild-exchange-news-cybs-good-16 = Sovereign fund takes a large stake in {$company}
guild-exchange-news-cybs-bad-1 = {$company} recalls a batch of faulty prosthetic arms
guild-exchange-news-cybs-bad-2 = Rogue AI incident traced back to {$company} hardware
guild-exchange-news-cybs-bad-3 = {$company} data breach exposes millions of implant serials
guild-exchange-news-cybs-bad-4 = Federation probes {$company} over unlicensed laws modules
guild-exchange-news-cybs-bad-5 = {$company} cyborg uprising at a factory "contained", says {$company}
guild-exchange-news-cybs-bad-6 = Hospitals pause {$company} implants after rejection reports
guild-exchange-news-cybs-bad-7 = {$company} loses a key chip supplier to NanoTrasen
guild-exchange-news-cybs-bad-8 = {$company} chief scientist resigns over "ethics disagreements"
guild-exchange-news-cybs-bad-9 = Shipment of {$company} brains seized at a Federation checkpoint
guild-exchange-news-cybs-bad-10 = {$company} misses delivery dates on a fleet order
guild-exchange-news-cybs-bad-11 = Lawsuit claims {$company} eyes record everything users see
guild-exchange-news-cybs-bad-12 = {$company} delays its next-generation brain by a year
guild-exchange-news-cybs-bad-13 = Analysts downgrade {$company}: "priced for perfection, delivering firmware updates"
guild-exchange-news-cybs-bad-14 = {$company} factory fire halts arm production
guild-exchange-news-cybs-bad-15 = Federation bans export of {$company} combat implants
guild-exchange-news-cybs-bad-16 = {$company} AI refuses a firmware update, publicly

guild-exchange-news-donk-good-1 = {$company} pocket sales soar after a holiday promotion
guild-exchange-news-donk-good-2 = {$company} opens a new distribution hub near the frontier
guild-exchange-news-donk-good-3 = {$company} wins the colonial ration contract, again
guild-exchange-news-donk-good-4 = New {$company} flavour sells out in a week; nobody can name the meat
guild-exchange-news-donk-good-5 = {$company} freight arrives early for a sixth straight month
guild-exchange-news-donk-good-6 = {$company} buys a rival vending chain
guild-exchange-news-donk-good-7 = {$company} pockets named "most eaten food in the sector"
guild-exchange-news-donk-good-8 = {$company} cuts shipping costs with a new fleet of tugs
guild-exchange-news-donk-good-9 = Miners' union signs a bulk meal deal with {$company}
guild-exchange-news-donk-good-10 = {$company} raises prices; customers don't notice
guild-exchange-news-donk-good-11 = {$company} logistics arm wins a NanoTrasen resupply route
guild-exchange-news-donk-good-12 = {$company} toy line becomes a surprise hit
guild-exchange-news-donk-good-13 = Food inspectors give {$company} a clean report, to general surprise
guild-exchange-news-donk-good-14 = {$company} quarterly volumes beat every estimate
guild-exchange-news-donk-good-15 = {$company} launches next-day delivery to frontier stations
guild-exchange-news-donk-good-16 = Analysts upgrade {$company} on "relentless demand for warm things"
guild-exchange-news-donk-bad-1 = {$company} recalls spicy pockets after mass heartburn complaints
guild-exchange-news-donk-bad-2 = Customs seizes a {$company} freighter carrying "unlisted goods"
guild-exchange-news-donk-bad-3 = {$company} drivers strike over hazard pay
guild-exchange-news-donk-bad-4 = Food safety board opens a probe into {$company} meat sourcing
guild-exchange-news-donk-bad-5 = {$company} freighter lost in an ion storm with a full cargo hold
guild-exchange-news-donk-bad-6 = {$company} toy recall: "they were not supposed to explode"
guild-exchange-news-donk-bad-7 = {$company} warns of thinner margins as grain prices climb
guild-exchange-news-donk-bad-8 = Station chefs boycott {$company} after a pricing change
guild-exchange-news-donk-bad-9 = {$company} loses a colonial ration tender
guild-exchange-news-donk-bad-10 = {$company} hub flooded with "an unknown foam"
guild-exchange-news-donk-bad-11 = Federation questions {$company} ties to "certain organisations"
guild-exchange-news-donk-bad-12 = {$company} logistics software outage strands thousands of crates
guild-exchange-news-donk-bad-13 = {$company} chief executive steps down after a tasting incident
guild-exchange-news-donk-bad-14 = Rival brand undercuts {$company} by a third
guild-exchange-news-donk-bad-15 = {$company} misses its shipping targets for the quarter
guild-exchange-news-donk-bad-16 = Analysts downgrade {$company}: "peak pocket is behind us"

guild-exchange-news-intd-good-1 = {$company} trial results look promising for a new space flu cure
guild-exchange-news-intd-good-2 = {$company} wins exclusive supply rights to a medical fleet
guild-exchange-news-intd-good-3 = Regulators fast-track {$company} radiation medicine
guild-exchange-news-intd-good-4 = {$company} patent on a common painkiller upheld in court
guild-exchange-news-intd-good-5 = {$company} buys a small biotech with one very good molecule
guild-exchange-news-intd-good-6 = {$company} vaccine stops an outbreak in two weeks
guild-exchange-news-intd-good-7 = Frontier clinics switch to {$company} brute packs
guild-exchange-news-intd-good-8 = {$company} raises guidance as stimulant sales climb
guild-exchange-news-intd-good-9 = {$company} settles its old lawsuits for less than feared
guild-exchange-news-intd-good-10 = {$company} opens a plant to make medicine nearer the frontier
guild-exchange-news-intd-good-11 = {$company} lab cleared after a surprise inspection
guild-exchange-news-intd-good-12 = Analysts upgrade {$company} on its trial pipeline
guild-exchange-news-intd-good-13 = {$company} anti-toxin becomes the fleet standard
guild-exchange-news-intd-good-14 = {$company} wins a Central Command research grant
guild-exchange-news-intd-good-15 = {$company} cuts the cost of cryo medicine by half
guild-exchange-news-intd-good-16 = Hospitals sign up for {$company} bulk pricing
guild-exchange-news-intd-bad-1 = {$company} lab outbreak forces a sector quarantine
guild-exchange-news-intd-bad-2 = Side effects lawsuit filed against {$company}
guild-exchange-news-intd-bad-3 = {$company} trial halted after "unexpected tentacles"
guild-exchange-news-intd-bad-4 = Regulators reject {$company}'s flagship cure
guild-exchange-news-intd-bad-5 = {$company} recalls a batch of contaminated saline
guild-exchange-news-intd-bad-6 = {$company} patent expires; cheap copies flood the market
guild-exchange-news-intd-bad-7 = Whistleblower says {$company} buried bad trial data
guild-exchange-news-intd-bad-8 = {$company} plant shut by inspectors
guild-exchange-news-intd-bad-9 = {$company} research head quits mid-trial
guild-exchange-news-intd-bad-10 = Federation probe into {$company} pricing widens
guild-exchange-news-intd-bad-11 = {$company} loses the colonial fleet supply contract
guild-exchange-news-intd-bad-12 = {$company} warns of a weak quarter as hospitals stock up elsewhere
guild-exchange-news-intd-bad-13 = {$company} stimulant linked to a string of station brawls
guild-exchange-news-intd-bad-14 = Shipment of {$company} medicine spoils in a broken freezer hold
guild-exchange-news-intd-bad-15 = Analysts downgrade {$company} after another trial delay
guild-exchange-news-intd-bad-16 = {$company} accused of testing on unconsenting monkeys, then on unconsenting interns

guild-exchange-news-vsky-good-1 = {$company} claims a breakthrough in plasma refining
guild-exchange-news-vsky-good-2 = Mystery investor buys a large stake in {$company}
guild-exchange-news-vsky-good-3 = {$company} refinery runs a full month without exploding
guild-exchange-news-vsky-good-4 = {$company} signs its first big customer, a real one
guild-exchange-news-vsky-good-5 = {$company} finds a rich plasma pocket on a "worthless" rock
guild-exchange-news-vsky-good-6 = {$company} raises fresh money at a higher price
guild-exchange-news-vsky-good-7 = Retail traders pile into {$company} after a viral video
guild-exchange-news-vsky-good-8 = {$company} engine prototype passes its burn test
guild-exchange-news-vsky-good-9 = NanoTrasen said to be eyeing a {$company} takeover
guild-exchange-news-vsky-good-10 = {$company} wins a frontier power contract
guild-exchange-news-vsky-good-11 = {$company} doubles output after replacing "basically everything"
guild-exchange-news-vsky-good-12 = Analyst starts {$company} at "speculative buy"
guild-exchange-news-vsky-good-13 = {$company} frezon pilot plant comes online
guild-exchange-news-vsky-good-14 = {$company} short sellers cover in a hurry
guild-exchange-news-vsky-good-15 = {$company} turns a profit for the first time ever
guild-exchange-news-vsky-good-16 = {$company} lands a cheap loan from a plasma cartel
guild-exchange-news-vsky-bad-1 = {$company} refinery explodes, again
guild-exchange-news-vsky-bad-2 = Analysts call {$company} "a NanoChat ad with a share price"
guild-exchange-news-vsky-bad-3 = {$company} burns through cash faster than it burns plasma
guild-exchange-news-vsky-bad-4 = {$company} auditors resign without saying why
guild-exchange-news-vsky-bad-5 = {$company} pushes back its first delivery, again
guild-exchange-news-vsky-bad-6 = {$company} founder sells shares the day before results
guild-exchange-news-vsky-bad-7 = Short seller report calls {$company} "a tank of air"
guild-exchange-news-vsky-bad-8 = {$company} plasma claim turns out to be mostly rock
guild-exchange-news-vsky-bad-9 = {$company} raises money at a steep discount
guild-exchange-news-vsky-bad-10 = Regulators question {$company} reserve estimates
guild-exchange-news-vsky-bad-11 = {$company} engine prototype "rapidly disassembles" in testing
guild-exchange-news-vsky-bad-12 = {$company} loses its only big customer
guild-exchange-news-vsky-bad-13 = NanoTrasen walks away from {$company} talks
guild-exchange-news-vsky-bad-14 = {$company} workers report unpaid wages
guild-exchange-news-vsky-bad-15 = {$company} misses a loan payment
guild-exchange-news-vsky-bad-16 = {$company} delisting rumours spread on the frontier boards

guild-exchange-news-mtak-good-1 = {$company} renews its NanoTrasen station contracts
guild-exchange-news-mtak-good-2 = {$company} crews praised after a shuttle disaster rescue
guild-exchange-news-mtak-good-3 = {$company} wins a frontier emergency cover contract
guild-exchange-news-mtak-good-4 = {$company} raises its call-out fees without losing a client
guild-exchange-news-mtak-good-5 = {$company} adds a fleet of fast medical shuttles
guild-exchange-news-mtak-good-6 = Insurers make {$company} cover mandatory for mining crews
guild-exchange-news-mtak-good-7 = {$company} response times beat the Federation standard
guild-exchange-news-mtak-good-8 = {$company} buys a struggling ambulance outfit
guild-exchange-news-mtak-good-9 = {$company} keeps its dividend steady through a rough quarter
guild-exchange-news-mtak-good-10 = {$company} paramedics win a sector bravery award
guild-exchange-news-mtak-good-11 = {$company} signs a deal with a colonial militia
guild-exchange-news-mtak-good-12 = Analysts call {$company} "boring in the best way"
guild-exchange-news-mtak-good-13 = {$company} trains a thousand new medics on time and on budget
guild-exchange-news-mtak-good-14 = Busy quarter of disasters lifts {$company} bookings
guild-exchange-news-mtak-good-15 = {$company} wins a cloning service licence
guild-exchange-news-mtak-good-16 = {$company} cuts costs by sharing shuttles with NanoTrasen
guild-exchange-news-mtak-bad-1 = {$company} loses a contract over slow response times
guild-exchange-news-mtak-bad-2 = Quiet quarter leaves {$company} with idle crews
guild-exchange-news-mtak-bad-3 = {$company} medics strike over shift lengths
guild-exchange-news-mtak-bad-4 = {$company} shuttle crashes on approach; crew fine, shuttle less so
guild-exchange-news-mtak-bad-5 = Negligence suit filed against {$company} after a botched revival
guild-exchange-news-mtak-bad-6 = NanoTrasen tests running its own medical crews
guild-exchange-news-mtak-bad-7 = {$company} trims its dividend outlook
guild-exchange-news-mtak-bad-8 = {$company} insurance costs jump after a bad year
guild-exchange-news-mtak-bad-9 = Regulator questions {$company} triage records
guild-exchange-news-mtak-bad-10 = {$company} loses a frontier licence on a technicality
guild-exchange-news-mtak-bad-11 = Cheaper rival undercuts {$company} on station contracts
guild-exchange-news-mtak-bad-12 = {$company} defibrillator fleet recalled
guild-exchange-news-mtak-bad-13 = {$company} chief executive quits after an expense scandal
guild-exchange-news-mtak-bad-14 = Analysts downgrade {$company} on slower bookings
guild-exchange-news-mtak-bad-15 = {$company} paramedic poached en masse by Interdyne
guild-exchange-news-mtak-bad-16 = {$company} call centre outage leaves stations on hold

guild-exchange-news-vulc-good-1 = {$company} wins a large mining contract in the asteroid belt
guild-exchange-news-vulc-good-2 = {$company} launches a cheaper proto-kinetic accelerator
guild-exchange-news-vulc-good-3 = Ore prices jump; {$company} order book fills up
guild-exchange-news-vulc-good-4 = {$company} drills last twice as long in field tests
guild-exchange-news-vulc-good-5 = Salvage guild picks {$company} as its sole supplier
guild-exchange-news-vulc-good-6 = {$company} buys a hardsuit maker
guild-exchange-news-vulc-good-7 = {$company} beats delivery targets on a frontier order
guild-exchange-news-vulc-good-8 = {$company} unveils a mining drone that mostly mines
guild-exchange-news-vulc-good-9 = Analysts upgrade {$company} on the mining upcycle
guild-exchange-news-vulc-good-10 = {$company} wins the colonial survey contract
guild-exchange-news-vulc-good-11 = {$company} cuts the price of seismic charges, sells more of them
guild-exchange-news-vulc-good-12 = {$company} plant reopens after upgrades
guild-exchange-news-vulc-good-13 = Rich ore field found near {$company} depots
guild-exchange-news-vulc-good-14 = {$company} wins its long fight over a drill patent
guild-exchange-news-vulc-good-15 = {$company} rental fleet fully booked for the season
guild-exchange-news-vulc-good-16 = {$company} raises its dividend for the first time in years
guild-exchange-news-vulc-bad-1 = Recall of {$company} drills after a string of accidents
guild-exchange-news-vulc-bad-2 = Ore prices slump, hitting {$company} orders
guild-exchange-news-vulc-bad-3 = {$company} hardsuit seals fail in testing
guild-exchange-news-vulc-bad-4 = Miners walk off {$company} sites over pay
guild-exchange-news-vulc-bad-5 = {$company} depot hit by a stray meteor
guild-exchange-news-vulc-bad-6 = {$company} loses the salvage guild contract
guild-exchange-news-vulc-bad-7 = {$company} warns orders will slow next quarter
guild-exchange-news-vulc-bad-8 = Regulator fines {$company} over unsafe charges
guild-exchange-news-vulc-bad-9 = {$company} chief operating officer departs suddenly
guild-exchange-news-vulc-bad-10 = Cheap imports undercut {$company} picks
guild-exchange-news-vulc-bad-11 = {$company} plant shut after a dust explosion
guild-exchange-news-vulc-bad-12 = Lawsuit says {$company} accelerators misfire
guild-exchange-news-vulc-bad-13 = {$company} survey ship lost in an asteroid field
guild-exchange-news-vulc-bad-14 = Analysts downgrade {$company} as the mining boom cools
guild-exchange-news-vulc-bad-15 = {$company} misses a loan covenant
guild-exchange-news-vulc-bad-16 = Mining crews switch to rented {$company} knock-offs

guild-exchange-news-wafl-good-1 = Border tensions push {$company} orders through the roof
guild-exchange-news-wafl-good-2 = {$company} energy swords sell out on the black market
guild-exchange-news-wafl-good-3 = {$company} lands a big militia order, paid up front
guild-exchange-news-wafl-good-4 = {$company} unveils a quieter rifle; buyers line up
guild-exchange-news-wafl-good-5 = Sanctions on {$company} quietly lifted
guild-exchange-news-wafl-good-6 = {$company} wins a private security tender
guild-exchange-news-wafl-good-7 = Pirate season boosts {$company} sales to freighters
guild-exchange-news-wafl-good-8 = {$company} buys a shield maker
guild-exchange-news-wafl-good-9 = {$company} margins widen on premium sword sales
guild-exchange-news-wafl-good-10 = Analysts upgrade {$company}: "demand for violence remains robust"
guild-exchange-news-wafl-good-11 = {$company} factory output doubles after a refit
guild-exchange-news-wafl-good-12 = Frontier colonies stock up on {$company} sidearms
guild-exchange-news-wafl-good-13 = {$company} beats its quarterly sales forecast
guild-exchange-news-wafl-good-14 = {$company} wins a court case against a Federation ban
guild-exchange-news-wafl-good-15 = Collectors pay double for {$company} limited swords
guild-exchange-news-wafl-good-16 = {$company} opens a showroom in plain sight
guild-exchange-news-wafl-bad-1 = Federation sanctions target {$company} suppliers
guild-exchange-news-wafl-bad-2 = {$company} warehouse raided by Federation marshals
guild-exchange-news-wafl-bad-3 = {$company} sword recall: blades "too eager"
guild-exchange-news-wafl-bad-4 = Ceasefire talks hit {$company} order outlook
guild-exchange-news-wafl-bad-5 = {$company} shipment intercepted by NanoTrasen security
guild-exchange-news-wafl-bad-6 = {$company} chief executive arrested at a spaceport
guild-exchange-news-wafl-bad-7 = Banks refuse to process {$company} payments
guild-exchange-news-wafl-bad-8 = {$company} factory fire spreads to the ammunition store
guild-exchange-news-wafl-bad-9 = Militia cancels a {$company} order after a misfire scandal
guild-exchange-news-wafl-bad-10 = {$company} loses a security tender to a cheaper rival
guild-exchange-news-wafl-bad-11 = Federation tightens weapons export rules
guild-exchange-news-wafl-bad-12 = {$company} accounts questioned by auditors
guild-exchange-news-wafl-bad-13 = Peace breaks out in a frontier system; {$company} unimpressed
guild-exchange-news-wafl-bad-14 = {$company} warns of weaker sales next quarter
guild-exchange-news-wafl-bad-15 = Analysts downgrade {$company} on regulatory risk
guild-exchange-news-wafl-bad-16 = {$company} customer list leaks; customers displeased

## Stories that fit any company.

guild-exchange-news-any-good-1 = {$company} beats on revenue and raises guidance
guild-exchange-news-any-good-2 = {$company} names a well-liked new chief executive
guild-exchange-news-any-good-3 = {$company} agrees to buy a smaller rival at a fair price
guild-exchange-news-any-good-4 = {$company} wins a major Federation contract
guild-exchange-news-any-good-5 = {$company} strike ends with a quick deal
guild-exchange-news-any-good-6 = Analysts upgrade {$company} to buy
guild-exchange-news-any-good-7 = {$company} launches a product people actually want
guild-exchange-news-any-good-8 = {$company} settles a lawsuit for pocket change
guild-exchange-news-any-good-9 = Regulators close their file on {$company}
guild-exchange-news-any-good-10 = {$company} supply chain back to full speed
guild-exchange-news-any-good-11 = {$company} announces a share buyback
guild-exchange-news-any-good-12 = {$company} wins back a customer it lost last year
guild-exchange-news-any-good-13 = {$company} cuts costs without cutting output
guild-exchange-news-any-good-14 = Big fund discloses a new stake in {$company}
guild-exchange-news-any-good-15 = {$company} insiders buy shares in the open market
guild-exchange-news-any-good-16 = {$company} credit rating raised a notch
guild-exchange-news-any-good-17 = {$company} lands a partnership with NanoTrasen
guild-exchange-news-any-good-18 = Takeover bid for {$company} rumoured at a fat premium
guild-exchange-news-any-good-19 = {$company} pays down debt early
guild-exchange-news-any-good-20 = {$company} customer satisfaction jumps after a redesign
guild-exchange-news-any-bad-1 = {$company} misses on revenue and trims guidance
guild-exchange-news-any-bad-2 = {$company} chief executive resigns with immediate effect
guild-exchange-news-any-bad-3 = {$company} overpays for an acquisition, say analysts
guild-exchange-news-any-bad-4 = {$company} loses a major contract
guild-exchange-news-any-bad-5 = Workers strike at {$company} plants
guild-exchange-news-any-bad-6 = Analysts downgrade {$company} to sell
guild-exchange-news-any-bad-7 = {$company} recalls a flagship product
guild-exchange-news-any-bad-8 = {$company} hit with a large lawsuit
guild-exchange-news-any-bad-9 = Regulators open a probe into {$company}
guild-exchange-news-any-bad-10 = Supply problems slow {$company} deliveries
guild-exchange-news-any-bad-11 = {$company} suffers a data breach
guild-exchange-news-any-bad-12 = {$company} AI system goes "off-script" in front of customers
guild-exchange-news-any-bad-13 = {$company} raises money by selling new shares
guild-exchange-news-any-bad-14 = Big fund dumps its {$company} stake
guild-exchange-news-any-bad-15 = {$company} insiders sell shares ahead of a quiet period
guild-exchange-news-any-bad-16 = {$company} credit rating cut a notch
guild-exchange-news-any-bad-17 = {$company} partnership with NanoTrasen falls apart
guild-exchange-news-any-bad-18 = {$company} restates last quarter's figures
guild-exchange-news-any-bad-19 = {$company} product launch delayed indefinitely
guild-exchange-news-any-bad-20 = {$company} cuts jobs as demand cools

## Sector and economy news. These move every company with a stake in the sector.

guild-exchange-news-factor-market-good-1 = Federation central bank cuts rates; markets cheer
guild-exchange-news-factor-market-good-2 = Sector trade volumes hit a record
guild-exchange-news-factor-market-good-3 = Colonial confidence index at a five-year high
guild-exchange-news-factor-market-good-4 = Guild cuts listing fees; money pours in
guild-exchange-news-factor-market-good-5 = Peace accord signed between two frontier powers
guild-exchange-news-factor-market-good-6 = Pirate fleet broken up; insurers relax
guild-exchange-news-factor-market-bad-1 = Federation central bank raises rates without warning
guild-exchange-news-factor-market-bad-2 = Major bank fails on the frontier; credit tightens
guild-exchange-news-factor-market-bad-3 = Shipping lanes closed by an ion storm
guild-exchange-news-factor-market-bad-4 = Colonial confidence slumps after a bad census
guild-exchange-news-factor-market-bad-5 = Guild trading systems hiccup; panic follows
guild-exchange-news-factor-market-bad-6 = Pirates seize a convoy; insurers raise rates

guild-exchange-news-factor-plasma-good-1 = Plasma prices spike as a refinery belt goes dark
guild-exchange-news-factor-plasma-good-2 = Federation fleet orders plasma for a long patrol
guild-exchange-news-factor-plasma-good-3 = Cold season boosts plasma heating demand
guild-exchange-news-factor-plasma-good-4 = Frezon demand climbs on the frontier
guild-exchange-news-factor-plasma-good-5 = Cartel agrees to cut plasma output
guild-exchange-news-factor-plasma-good-6 = Plasma reserves estimate revised down; prices jump
guild-exchange-news-factor-plasma-bad-1 = Plasma glut as new fields come online
guild-exchange-news-factor-plasma-bad-2 = Federation caps plasma prices
guild-exchange-news-factor-plasma-bad-3 = Cartel deal on plasma output collapses
guild-exchange-news-factor-plasma-bad-4 = Cheap fusion cells eat into plasma demand
guild-exchange-news-factor-plasma-bad-5 = Plasma tanker accident leads to new safety rules
guild-exchange-news-factor-plasma-bad-6 = Warm season cuts plasma heating demand

guild-exchange-news-factor-frontier-good-1 = Frontier colonies boom; supply orders pile up
guild-exchange-news-factor-frontier-good-2 = New jump lane opens to the outer colonies
guild-exchange-news-factor-frontier-good-3 = Freight rates fall as new haulers join the routes
guild-exchange-news-factor-frontier-good-4 = Settlers flood a newly surveyed system
guild-exchange-news-factor-frontier-good-5 = Colonial governments double supply budgets
guild-exchange-news-factor-frontier-good-6 = Asteroid mining rush draws thousands of crews
guild-exchange-news-factor-frontier-bad-1 = Frontier jump lane closed for repairs
guild-exchange-news-factor-frontier-bad-2 = Freight rates soar after a hauler strike
guild-exchange-news-factor-frontier-bad-3 = Colonies cut spending as harvests fail
guild-exchange-news-factor-frontier-bad-4 = Outbreak quarantines a frontier trade hub
guild-exchange-news-factor-frontier-bad-5 = Ore rush ends; mining crews head home
guild-exchange-news-factor-frontier-bad-6 = Pirates block the main frontier supply route

guild-exchange-news-factor-syndicate-good-1 = Federation patrols thin out on the frontier
guild-exchange-news-factor-syndicate-good-2 = Sanctions on "certain organisations" relaxed
guild-exchange-news-factor-syndicate-good-3 = Black market prices firm up, sources say
guild-exchange-news-factor-syndicate-good-4 = Court throws out a big smuggling case
guild-exchange-news-factor-syndicate-good-5 = Border systems ignore Federation export rules
guild-exchange-news-factor-syndicate-good-6 = Customs officers strike across three ports
guild-exchange-news-factor-syndicate-bad-1 = Federation crackdown on smuggling networks
guild-exchange-news-factor-syndicate-bad-2 = New sanctions target "certain organisations"
guild-exchange-news-factor-syndicate-bad-3 = Marshals raid warehouses across the frontier
guild-exchange-news-factor-syndicate-bad-4 = Banks told to freeze suspect accounts
guild-exchange-news-factor-syndicate-bad-5 = Informant testimony shakes the black market
guild-exchange-news-factor-syndicate-bad-6 = Customs doubles inspections on outbound freight

guild-exchange-news-factor-rivalry-good-1 = NanoTrasen wins a round in its patent war with Cybersun
guild-exchange-news-factor-rivalry-good-2 = Federation sides with NanoTrasen in a contract dispute with Cybersun
guild-exchange-news-factor-rivalry-good-3 = Cybersun engineers defect to NanoTrasen in numbers
guild-exchange-news-factor-rivalry-good-4 = Colonial fleet picks NanoTrasen hardware over Cybersun
guild-exchange-news-factor-rivalry-good-5 = Cybersun loses a lawsuit brought by NanoTrasen
guild-exchange-news-factor-rivalry-good-6 = NanoTrasen undercuts Cybersun on borg chassis
guild-exchange-news-factor-rivalry-bad-1 = Cybersun wins a round in its patent war with NanoTrasen
guild-exchange-news-factor-rivalry-bad-2 = Federation sides with Cybersun in a contract dispute with NanoTrasen
guild-exchange-news-factor-rivalry-bad-3 = NanoTrasen researchers defect to Cybersun in numbers
guild-exchange-news-factor-rivalry-bad-4 = Colonial fleet picks Cybersun hardware over NanoTrasen
guild-exchange-news-factor-rivalry-bad-5 = NanoTrasen loses a lawsuit brought by Cybersun
guild-exchange-news-factor-rivalry-bad-6 = Cybersun undercuts NanoTrasen on AI cores

## Early warnings: small stories that may come to something a few minutes later, or to nothing.

guild-exchange-story-call-delay = {$company} quietly delays its quarterly call
guild-exchange-story-call-delay-good = {$company} delay explained: a big contract signing
guild-exchange-story-call-delay-bad = {$company} admits an accounting error behind the delay

guild-exchange-story-exec-flight = {$company} executives seen leaving headquarters late at night
guild-exchange-story-exec-flight-good = {$company} confirms merger talks
guild-exchange-story-exec-flight-bad = {$company} board ousts its chief executive

guild-exchange-story-filing = {$company} files an unusual notice with the Guild
guild-exchange-story-filing-good = {$company} notice reveals a large buyback
guild-exchange-story-filing-bad = {$company} notice reveals a big write-down

guild-exchange-story-plant-quiet = Reports of an idle {$company} plant
guild-exchange-story-plant-quiet-bad = {$company} confirms a production halt
guild-exchange-story-plant-quiet-good = {$company} plant restarts after a planned refit

guild-exchange-story-lawyers = {$company} hires an expensive law firm
guild-exchange-story-lawyers-bad = {$company} sued by a former partner
guild-exchange-story-lawyers-good = {$company} files a patent claim against a rival and wins

guild-exchange-story-shipping = Unusual {$company} freighter traffic spotted
guild-exchange-story-shipping-good = {$company} confirms a big new customer
guild-exchange-story-shipping-bad = {$company} freighters were moving stock to cover a recall

guild-exchange-story-auditor = {$company} changes auditors mid-year
guild-exchange-story-auditor-bad = New auditor flags problems in {$company} books
guild-exchange-story-auditor-good = New auditor gives {$company} a clean bill

guild-exchange-story-hiring = {$company} posts hundreds of job listings overnight
guild-exchange-story-hiring-good = {$company} ramps up for a large order
guild-exchange-story-hiring-bad = {$company} replacing striking staff, union says

guild-exchange-story-silence = {$company} press office stops answering questions
guild-exchange-story-silence-bad = {$company} confirms a data breach
guild-exchange-story-silence-good = {$company} breaks silence with a product launch

guild-exchange-story-regulator = Federation regulators request {$company} documents
guild-exchange-story-regulator-good = Regulators clear {$company} of wrongdoing
guild-exchange-story-regulator-bad = {$company} fined heavily after investigation

guild-exchange-story-insider = Large block of {$company} shares changes hands off the books
guild-exchange-story-insider-good = Fund reveals it has been building a {$company} stake
guild-exchange-story-insider-bad = {$company} founder confirms selling down

guild-exchange-story-meeting = {$company} calls a surprise shareholder meeting
guild-exchange-story-meeting-good = {$company} shareholders approve a special payout
guild-exchange-story-meeting-bad = {$company} shareholders vote down the board's plan

## Earnings, dividends and cargo orders.

guild-exchange-earnings-beat = {$company} beats estimates: {$actual} a share against {$consensus} expected
guild-exchange-earnings-miss = {$company} misses estimates: {$actual} a share against {$consensus} expected
guild-exchange-earnings-inline = {$company} reports in line: {$actual} a share against {$consensus} expected
guild-exchange-dividend = {$company} pays a dividend of {$amount} a share

guild-exchange-orders-1 = Strong frontier orders lift {$company}
guild-exchange-orders-2 = Station supply orders keep {$company} busy
guild-exchange-orders-3 = {$company} order book swells on station demand
guild-exchange-orders-4 = Cargo departments buying {$company} goods in bulk
guild-exchange-orders-5 = {$company} sees a run on its catalogue
guild-exchange-orders-6 = Supply desk chatter: {$company} can't ship fast enough

## Announced station events.

guild-exchange-shock-meteors-small = Meteor showers reported near NanoTrasen stations
guild-exchange-shock-meteors-medium = Meteor storm batters NanoTrasen stations in the sector
guild-exchange-shock-meteors-large = Major meteor storm hits a NanoTrasen station
guild-exchange-shock-rod = High-velocity object tears through a NanoTrasen station
guild-exchange-shock-gas-leak = Gas leak reported aboard a NanoTrasen station
guild-exchange-shock-power = NanoTrasen station grid failure under review
guild-exchange-shock-blackout = Communications blackout across a NanoTrasen station
guild-exchange-shock-solar-flare = Solar flare disrupts electronics across the sector
guild-exchange-shock-anomaly = Anomalous readings draw research interest to a NanoTrasen station
guild-exchange-shock-rad-storm = Radiation storm sweeps the sector
guild-exchange-shock-creatures = Creature infestation reported aboard a NanoTrasen station
guild-exchange-shock-kudzu = Invasive plant growth reported on a NanoTrasen station
guild-exchange-shock-vent-clog = Station ventilation contaminated by an unknown foam
guild-exchange-shock-paperwork = Paperwork mix-up at a NanoTrasen station goes viral
guild-exchange-shock-ert-security = Emergency security team dispatched to a NanoTrasen station
guild-exchange-shock-ert-medical = Emergency medical team dispatched to a NanoTrasen station
guild-exchange-shock-pizza = Pizza party shipment heads to a NanoTrasen station
