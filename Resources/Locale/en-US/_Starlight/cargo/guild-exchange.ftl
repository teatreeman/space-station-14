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
guild-exchange-window-source-station = SN
guild-exchange-window-source-fast = FW
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

## What an order did, shown to the trader.

guild-exchange-notice-bought = Bought {$shares} {$ticker} for ₷{$money}
guild-exchange-notice-sold = Sold {$shares} {$ticker} for ₷{$money}
guild-exchange-notice-shorted = Shorted {$shares} {$ticker} at ₷{$price}, ₷{$money} margin held
guild-exchange-notice-covered = Covered {$shares} {$ticker} at ₷{$price}, ₷{$money} returned
guild-exchange-notice-covered-paid = Covered {$shares} {$ticker} at ₷{$price}, ₷{$money} more paid
guild-exchange-notice-limit-buy = Limit order placed: buy {$shares} {$ticker} at ₷{$limit}
guild-exchange-notice-limit-sell = Limit order placed: sell {$shares} {$ticker} at ₷{$limit}

## The trade tape.

guild-exchange-cargo-trader = Cargo
guild-exchange-log-bought = {$trader} bought {$shares} {$ticker} for ₷{$money}
guild-exchange-log-sold = {$trader} sold {$shares} {$ticker} for ₷{$money}
guild-exchange-log-shorted = {$trader} shorted {$shares} {$ticker} at ₷{$price}
guild-exchange-log-covered = {$trader} covered {$shares} {$ticker} at ₷{$price}
guild-exchange-log-margin-call = MARGIN CALL at {$time}: {$trader} covered {$shares} {$ticker} at ₷{$price}

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
guild-exchange-company-cybs = Cybersun Industries
guild-exchange-company-donk = Donk Co.
guild-exchange-company-intd = Interdyne Pharmaceuticals
guild-exchange-company-vsky = Violet Sky Systems
guild-exchange-company-mtak = MedTak Interstellar
guild-exchange-company-vulc = Vulcan Industries
guild-exchange-company-wafl = Waffle Co.

## Company news. Each company has its own good and bad lines; {$company} is its ticker.
## Headlines are wire flashes: subject first, present tense, no articles, no full stops.

guild-exchange-news-nt-good-1 = {$company} posts record quarterly plasma output
guild-exchange-news-nt-good-2 = {$company} wins Federation station-building contract
guild-exchange-news-nt-good-3 = {$company} raises full-year outlook on refinery margins
guild-exchange-news-nt-good-4 = {$company} board approves multibillion share buyback
guild-exchange-news-nt-good-5 = {$company} insurance claims fall for 3rd straight quarter
guild-exchange-news-nt-good-6 = {$company} buys bankrupt shipyard at scrap prices
guild-exchange-news-nt-good-7 = CentCom approves six new {$company} research outposts
guild-exchange-news-nt-good-8 = Federation drops antitrust case against {$company}
guild-exchange-news-nt-good-9 = {$company} crew retention at 10-year high; analysts baffled
guild-exchange-news-nt-good-10 = {$company} plasma futures sold out through next cycle
guild-exchange-news-nt-good-11 = Analysts upgrade {$company} to 'buy, obviously'
guild-exchange-news-nt-good-12 = {$company} closes longest-running loss-making station
guild-exchange-news-nt-good-13 = {$company} signs 10-year supply deal with colonial fleet
guild-exchange-news-nt-good-14 = New {$company} bluespace relay halves shipping times
guild-exchange-news-nt-good-15 = {$company} wins crew wage claim in arbitration
guild-exchange-news-nt-good-16 = Pension funds pile into {$company} after calm quarter
guild-exchange-news-nt-bad-1 = {$company} loses station to supermatter accident
guild-exchange-news-nt-bad-2 = Holders question {$company} safety record after crew strike
guild-exchange-news-nt-bad-3 = {$company} cuts guidance; cites 'unforeseen station events'
guild-exchange-news-nt-bad-4 = {$company} plasma shipment vanishes past frontier
guild-exchange-news-nt-bad-5 = Federation opens inquiry into {$company} crew contracts
guild-exchange-news-nt-bad-6 = {$company} recalls emergency oxygen tanks
guild-exchange-news-nt-bad-7 = {$company} clone memory-loss class action goes to trial
guild-exchange-news-nt-bad-8 = {$company} research station goes dark; CentCom silent
guild-exchange-news-nt-bad-9 = Credit agency puts {$company} on negative watch
guild-exchange-news-nt-bad-10 = {$company} loses colonial contract to cheaper rival
guild-exchange-news-nt-bad-11 = Leaked memo shows {$company} skipped year of inspections
guild-exchange-news-nt-bad-12 = {$company} cuts security budget; insurers object
guild-exchange-news-nt-bad-13 = Dock workers walk out at three {$company} ports
guild-exchange-news-nt-bad-14 = {$company} writes off half-built station
guild-exchange-news-nt-bad-15 = {$company} CFO quits 'to spend time with family'
guild-exchange-news-nt-bad-16 = Plasma glut squeezes {$company} refinery margins

guild-exchange-news-cybs-good-1 = {$company} unveils new positronic brain line
guild-exchange-news-cybs-good-2 = {$company} signs implant deal with 3 Federation hospitals
guild-exchange-news-cybs-good-3 = {$company} implant orders double in quarter
guild-exchange-news-cybs-good-4 = {$company} poaches NanoTrasen research chief
guild-exchange-news-cybs-good-5 = {$company} AI clears Federation safety check at first try
guild-exchange-news-cybs-good-6 = Militia fleet orders {$company} targeting cores in bulk
guild-exchange-news-cybs-good-7 = {$company} prosthetics win design prize; waiting list grows
guild-exchange-news-cybs-good-8 = {$company} wins synthetic-eye patent fight
guild-exchange-news-cybs-good-9 = {$company} buys borg chassis maker for cash
guild-exchange-news-cybs-good-10 = Analysts call {$company} 'only cyborg stock that matters'
guild-exchange-news-cybs-good-11 = {$company} cuts implant prices, still widens margins
guild-exchange-news-cybs-good-12 = {$company} wins long-term frontier medbay contract
guild-exchange-news-cybs-good-13 = {$company} opens research campus next door to NanoTrasen
guild-exchange-news-cybs-good-14 = {$company} mantis blade demand outstrips supply
guild-exchange-news-cybs-good-15 = {$company} beta firmware fixes twitching, users say
guild-exchange-news-cybs-good-16 = Sovereign fund takes large stake in {$company}
guild-exchange-news-cybs-bad-1 = {$company} recalls faulty prosthetic arms
guild-exchange-news-cybs-bad-2 = Rogue AI incident traced to {$company} hardware
guild-exchange-news-cybs-bad-3 = {$company} breach exposes millions of implant serials
guild-exchange-news-cybs-bad-4 = Federation probes {$company} over unlicensed laws modules
guild-exchange-news-cybs-bad-5 = {$company} says factory cyborg uprising 'contained'
guild-exchange-news-cybs-bad-6 = Hospitals pause {$company} implants on rejection reports
guild-exchange-news-cybs-bad-7 = {$company} loses key chip supplier to NanoTrasen
guild-exchange-news-cybs-bad-8 = {$company} chief scientist quits over 'ethics disagreements'
guild-exchange-news-cybs-bad-9 = Federation checkpoint seizes {$company} brain shipment
guild-exchange-news-cybs-bad-10 = {$company} misses delivery dates on fleet order
guild-exchange-news-cybs-bad-11 = Suit claims {$company} eyes record everything users see
guild-exchange-news-cybs-bad-12 = {$company} pushes next-generation brain back a year
guild-exchange-news-cybs-bad-13 = Analysts cut {$company}; 'priced for perfection'
guild-exchange-news-cybs-bad-14 = Factory fire halts {$company} arm production
guild-exchange-news-cybs-bad-15 = Federation bans {$company} combat implant exports
guild-exchange-news-cybs-bad-16 = {$company} AI publicly refuses firmware update

guild-exchange-news-donk-good-1 = {$company} holiday pocket sales soar
guild-exchange-news-donk-good-2 = {$company} opens frontier distribution hub
guild-exchange-news-donk-good-3 = {$company} wins colonial ration contract again
guild-exchange-news-donk-good-4 = New {$company} flavour sells out in week; meat unidentified
guild-exchange-news-donk-good-5 = {$company} freight early for 6th straight month
guild-exchange-news-donk-good-6 = {$company} buys rival vending chain
guild-exchange-news-donk-good-7 = {$company} pockets named sector's most eaten food
guild-exchange-news-donk-good-8 = New tug fleet cuts {$company} shipping costs
guild-exchange-news-donk-good-9 = Miners' union signs bulk meal deal with {$company}
guild-exchange-news-donk-good-10 = {$company} raises prices; customers don't notice
guild-exchange-news-donk-good-11 = {$company} logistics arm wins NanoTrasen resupply route
guild-exchange-news-donk-good-12 = {$company} toy line becomes surprise hit
guild-exchange-news-donk-good-13 = Food inspectors clear {$company}, to general surprise
guild-exchange-news-donk-good-14 = {$company} quarterly volumes beat every estimate
guild-exchange-news-donk-good-15 = {$company} launches next-day frontier delivery
guild-exchange-news-donk-good-16 = {$company} upgraded on 'relentless demand for warm things'
guild-exchange-news-donk-bad-1 = {$company} recalls spicy pockets over heartburn complaints
guild-exchange-news-donk-bad-2 = Customs seizes {$company} freighter; 'unlisted goods' cited
guild-exchange-news-donk-bad-3 = {$company} drivers strike over hazard pay
guild-exchange-news-donk-bad-4 = Food safety board probes {$company} meat sourcing
guild-exchange-news-donk-bad-5 = {$company} freighter lost in ion storm with full hold
guild-exchange-news-donk-bad-6 = {$company} recalls toys; 'not supposed to explode'
guild-exchange-news-donk-bad-7 = {$company} warns on margins as grain prices climb
guild-exchange-news-donk-bad-8 = Station chefs boycott {$company} over pricing change
guild-exchange-news-donk-bad-9 = {$company} loses colonial ration tender
guild-exchange-news-donk-bad-10 = {$company} hub flooded with 'unknown foam'
guild-exchange-news-donk-bad-11 = Federation questions {$company} ties to 'certain organisations'
guild-exchange-news-donk-bad-12 = {$company} software outage strands thousands of crates
guild-exchange-news-donk-bad-13 = {$company} CEO steps down after tasting incident
guild-exchange-news-donk-bad-14 = Rival brand undercuts {$company} by a third
guild-exchange-news-donk-bad-15 = {$company} misses quarterly shipping targets
guild-exchange-news-donk-bad-16 = {$company} downgraded; 'peak pocket is behind us'

guild-exchange-news-intd-good-1 = {$company} space flu cure shows promise in trial
guild-exchange-news-intd-good-2 = {$company} wins exclusive medical fleet supply rights
guild-exchange-news-intd-good-3 = Regulators fast-track {$company} radiation medicine
guild-exchange-news-intd-good-4 = Court upholds {$company} painkiller patent
guild-exchange-news-intd-good-5 = {$company} buys small biotech with one very good molecule
guild-exchange-news-intd-good-6 = {$company} vaccine stops outbreak in two weeks
guild-exchange-news-intd-good-7 = Frontier clinics switch to {$company} brute packs
guild-exchange-news-intd-good-8 = {$company} raises guidance as stimulant sales climb
guild-exchange-news-intd-good-9 = {$company} settles old lawsuits for less than feared
guild-exchange-news-intd-good-10 = {$company} to open medicine plant nearer frontier
guild-exchange-news-intd-good-11 = {$company} lab cleared in surprise inspection
guild-exchange-news-intd-good-12 = Analysts upgrade {$company} on trial pipeline
guild-exchange-news-intd-good-13 = {$company} anti-toxin becomes fleet standard
guild-exchange-news-intd-good-14 = {$company} wins CentCom research grant
guild-exchange-news-intd-good-15 = {$company} halves cost of cryo medicine
guild-exchange-news-intd-good-16 = Hospitals sign up for {$company} bulk pricing
guild-exchange-news-intd-bad-1 = {$company} lab outbreak forces sector quarantine
guild-exchange-news-intd-bad-2 = {$company} hit with side-effects lawsuit
guild-exchange-news-intd-bad-3 = {$company} trial halted after 'unexpected tentacles'
guild-exchange-news-intd-bad-4 = Regulators reject {$company} flagship cure
guild-exchange-news-intd-bad-5 = {$company} recalls contaminated saline
guild-exchange-news-intd-bad-6 = {$company} patent expires; cheap copies flood market
guild-exchange-news-intd-bad-7 = Whistleblower says {$company} buried bad trial data
guild-exchange-news-intd-bad-8 = Inspectors shut {$company} plant
guild-exchange-news-intd-bad-9 = {$company} research head quits mid-trial
guild-exchange-news-intd-bad-10 = Federation widens {$company} pricing probe
guild-exchange-news-intd-bad-11 = {$company} loses colonial fleet supply contract
guild-exchange-news-intd-bad-12 = {$company} sees weak quarter as hospitals buy elsewhere
guild-exchange-news-intd-bad-13 = {$company} stimulant linked to string of station brawls
guild-exchange-news-intd-bad-14 = {$company} medicine shipment spoils in failed freezer
guild-exchange-news-intd-bad-15 = Analysts downgrade {$company} after another trial delay
guild-exchange-news-intd-bad-16 = {$company} accused of testing on unwilling monkeys, interns

guild-exchange-news-vsky-good-1 = {$company} claims plasma refining breakthrough
guild-exchange-news-vsky-good-2 = Mystery investor takes large stake in {$company}
guild-exchange-news-vsky-good-3 = {$company} refinery goes full month without exploding
guild-exchange-news-vsky-good-4 = {$company} signs first big customer, a real one
guild-exchange-news-vsky-good-5 = {$company} finds rich plasma pocket on 'worthless' rock
guild-exchange-news-vsky-good-6 = {$company} raises fresh cash at higher price
guild-exchange-news-vsky-good-7 = Retail traders pile into {$company} after viral video
guild-exchange-news-vsky-good-8 = {$company} engine prototype passes burn test
guild-exchange-news-vsky-good-9 = NanoTrasen said to eye {$company} takeover
guild-exchange-news-vsky-good-10 = {$company} wins frontier power contract
guild-exchange-news-vsky-good-11 = {$company} doubles output after replacing 'basically everything'
guild-exchange-news-vsky-good-12 = Analyst starts {$company} at 'speculative buy'
guild-exchange-news-vsky-good-13 = {$company} frezon pilot plant comes online
guild-exchange-news-vsky-good-14 = {$company} short sellers scramble to cover
guild-exchange-news-vsky-good-15 = {$company} posts first-ever profit
guild-exchange-news-vsky-good-16 = {$company} lands cheap loan from plasma cartel
guild-exchange-news-vsky-bad-1 = {$company} refinery explodes again
guild-exchange-news-vsky-bad-2 = Analysts call {$company} 'NanoChat ad with a share price'
guild-exchange-news-vsky-bad-3 = {$company} burns cash faster than it burns plasma
guild-exchange-news-vsky-bad-4 = {$company} auditors resign, give no reason
guild-exchange-news-vsky-bad-5 = {$company} delays first delivery again
guild-exchange-news-vsky-bad-6 = {$company} founder sold shares day before results
guild-exchange-news-vsky-bad-7 = Short seller calls {$company} 'a tank of air'
guild-exchange-news-vsky-bad-8 = {$company} plasma claim turns out to be mostly rock
guild-exchange-news-vsky-bad-9 = {$company} raises cash at steep discount
guild-exchange-news-vsky-bad-10 = Regulators question {$company} reserve estimates
guild-exchange-news-vsky-bad-11 = {$company} engine prototype 'rapidly disassembles' in test
guild-exchange-news-vsky-bad-12 = {$company} loses only big customer
guild-exchange-news-vsky-bad-13 = NanoTrasen walks away from {$company} talks
guild-exchange-news-vsky-bad-14 = {$company} workers report unpaid wages
guild-exchange-news-vsky-bad-15 = {$company} misses loan payment
guild-exchange-news-vsky-bad-16 = {$company} delisting rumours spread on frontier boards

guild-exchange-news-mtak-good-1 = {$company} renews NanoTrasen station contracts
guild-exchange-news-mtak-good-2 = {$company} crews praised for shuttle disaster rescue
guild-exchange-news-mtak-good-3 = {$company} wins frontier emergency cover contract
guild-exchange-news-mtak-good-4 = {$company} raises call-out fees, keeps every client
guild-exchange-news-mtak-good-5 = {$company} adds fleet of fast medical shuttles
guild-exchange-news-mtak-good-6 = Insurers make {$company} cover mandatory for mining crews
guild-exchange-news-mtak-good-7 = {$company} response times beat Federation standard
guild-exchange-news-mtak-good-8 = {$company} buys struggling ambulance outfit
guild-exchange-news-mtak-good-9 = {$company} holds dividend through rough quarter
guild-exchange-news-mtak-good-10 = {$company} paramedics win sector bravery award
guild-exchange-news-mtak-good-11 = {$company} signs colonial militia deal
guild-exchange-news-mtak-good-12 = Analysts call {$company} 'boring in the best way'
guild-exchange-news-mtak-good-13 = {$company} trains 1,000 new medics on time, on budget
guild-exchange-news-mtak-good-14 = Busy quarter of disasters lifts {$company} bookings
guild-exchange-news-mtak-good-15 = {$company} wins cloning service licence
guild-exchange-news-mtak-good-16 = {$company} to share shuttles with NanoTrasen; costs fall
guild-exchange-news-mtak-bad-1 = {$company} loses contract over slow response times
guild-exchange-news-mtak-bad-2 = Quiet quarter leaves {$company} crews idle
guild-exchange-news-mtak-bad-3 = {$company} medics strike over shift lengths
guild-exchange-news-mtak-bad-4 = {$company} shuttle crashes on approach; crew unhurt
guild-exchange-news-mtak-bad-5 = {$company} sued for negligence over botched revival
guild-exchange-news-mtak-bad-6 = NanoTrasen to trial its own medical crews
guild-exchange-news-mtak-bad-7 = {$company} trims dividend outlook
guild-exchange-news-mtak-bad-8 = {$company} insurance costs jump after bad year
guild-exchange-news-mtak-bad-9 = Regulator questions {$company} triage records
guild-exchange-news-mtak-bad-10 = {$company} loses frontier licence on technicality
guild-exchange-news-mtak-bad-11 = Cheaper rival undercuts {$company} on station contracts
guild-exchange-news-mtak-bad-12 = {$company} recalls defibrillator fleet
guild-exchange-news-mtak-bad-13 = {$company} CEO quits over expense scandal
guild-exchange-news-mtak-bad-14 = Analysts downgrade {$company} on slower bookings
guild-exchange-news-mtak-bad-15 = Interdyne poaches {$company} paramedics en masse
guild-exchange-news-mtak-bad-16 = {$company} call centre outage leaves stations on hold

guild-exchange-news-vulc-good-1 = {$company} wins large asteroid belt mining contract
guild-exchange-news-vulc-good-2 = {$company} launches cheaper proto-kinetic accelerator
guild-exchange-news-vulc-good-3 = Ore prices jump; {$company} order book fills
guild-exchange-news-vulc-good-4 = {$company} drills last twice as long in field tests
guild-exchange-news-vulc-good-5 = Salvage guild names {$company} sole supplier
guild-exchange-news-vulc-good-6 = {$company} buys hardsuit maker
guild-exchange-news-vulc-good-7 = {$company} beats delivery targets on frontier order
guild-exchange-news-vulc-good-8 = {$company} unveils mining drone that mostly mines
guild-exchange-news-vulc-good-9 = Analysts upgrade {$company} on mining upcycle
guild-exchange-news-vulc-good-10 = {$company} wins colonial survey contract
guild-exchange-news-vulc-good-11 = {$company} cuts seismic charge prices, sells more
guild-exchange-news-vulc-good-12 = {$company} plant reopens after upgrades
guild-exchange-news-vulc-good-13 = Rich ore field found near {$company} depots
guild-exchange-news-vulc-good-14 = {$company} wins long fight over drill patent
guild-exchange-news-vulc-good-15 = {$company} rental fleet booked out for season
guild-exchange-news-vulc-good-16 = {$company} raises dividend for first time in years
guild-exchange-news-vulc-bad-1 = {$company} recalls drills after string of accidents
guild-exchange-news-vulc-bad-2 = Ore price slump hits {$company} orders
guild-exchange-news-vulc-bad-3 = {$company} hardsuit seals fail in testing
guild-exchange-news-vulc-bad-4 = Miners walk off {$company} sites over pay
guild-exchange-news-vulc-bad-5 = Stray meteor hits {$company} depot
guild-exchange-news-vulc-bad-6 = {$company} loses salvage guild contract
guild-exchange-news-vulc-bad-7 = {$company} sees slower orders next quarter
guild-exchange-news-vulc-bad-8 = Regulator fines {$company} over unsafe charges
guild-exchange-news-vulc-bad-9 = {$company} COO departs suddenly
guild-exchange-news-vulc-bad-10 = Cheap imports undercut {$company} picks
guild-exchange-news-vulc-bad-11 = Dust explosion shuts {$company} plant
guild-exchange-news-vulc-bad-12 = Suit says {$company} accelerators misfire
guild-exchange-news-vulc-bad-13 = {$company} survey ship lost in asteroid field
guild-exchange-news-vulc-bad-14 = Analysts downgrade {$company} as mining boom cools
guild-exchange-news-vulc-bad-15 = {$company} breaches loan covenant
guild-exchange-news-vulc-bad-16 = Mining crews switch to rented {$company} knock-offs

guild-exchange-news-wafl-good-1 = Border tensions send {$company} orders soaring
guild-exchange-news-wafl-good-2 = {$company} energy swords sell out on black market
guild-exchange-news-wafl-good-3 = {$company} lands big militia order, paid up front
guild-exchange-news-wafl-good-4 = {$company} unveils quieter rifle; buyers line up
guild-exchange-news-wafl-good-5 = Sanctions on {$company} quietly lifted
guild-exchange-news-wafl-good-6 = {$company} wins private security tender
guild-exchange-news-wafl-good-7 = Pirate season lifts {$company} sales to freighters
guild-exchange-news-wafl-good-8 = {$company} buys shield maker
guild-exchange-news-wafl-good-9 = Premium sword sales widen {$company} margins
guild-exchange-news-wafl-good-10 = {$company} upgraded; 'demand for violence remains robust'
guild-exchange-news-wafl-good-11 = {$company} output doubles after factory refit
guild-exchange-news-wafl-good-12 = Frontier colonies stock up on {$company} sidearms
guild-exchange-news-wafl-good-13 = {$company} beats quarterly sales forecast
guild-exchange-news-wafl-good-14 = {$company} wins court fight over Federation ban
guild-exchange-news-wafl-good-15 = Collectors pay double for limited {$company} swords
guild-exchange-news-wafl-good-16 = {$company} opens showroom in plain sight
guild-exchange-news-wafl-bad-1 = Federation sanctions hit {$company} suppliers
guild-exchange-news-wafl-bad-2 = Federation marshals raid {$company} warehouse
guild-exchange-news-wafl-bad-3 = {$company} recalls swords; blades 'too eager'
guild-exchange-news-wafl-bad-4 = Ceasefire talks cloud {$company} order outlook
guild-exchange-news-wafl-bad-5 = NanoTrasen security intercepts {$company} shipment
guild-exchange-news-wafl-bad-6 = {$company} CEO arrested at spaceport
guild-exchange-news-wafl-bad-7 = Banks refuse to process {$company} payments
guild-exchange-news-wafl-bad-8 = {$company} factory fire spreads to ammunition store
guild-exchange-news-wafl-bad-9 = Militia cancels {$company} order over misfire scandal
guild-exchange-news-wafl-bad-10 = {$company} loses security tender to cheaper rival
guild-exchange-news-wafl-bad-11 = Federation tightens weapons export rules
guild-exchange-news-wafl-bad-12 = Auditors question {$company} accounts
guild-exchange-news-wafl-bad-13 = Peace breaks out in frontier system; {$company} unimpressed
guild-exchange-news-wafl-bad-14 = {$company} sees weaker sales next quarter
guild-exchange-news-wafl-bad-15 = Analysts downgrade {$company} on regulatory risk
guild-exchange-news-wafl-bad-16 = {$company} customer list leaks; customers displeased

## Stories that fit any company.

guild-exchange-news-any-good-1 = {$company} beats on revenue, raises guidance
guild-exchange-news-any-good-2 = {$company} names well-liked new CEO
guild-exchange-news-any-good-3 = {$company} to buy smaller rival at fair price
guild-exchange-news-any-good-4 = {$company} wins major Federation contract
guild-exchange-news-any-good-5 = {$company} strike ends in quick deal
guild-exchange-news-any-good-6 = Analysts upgrade {$company} to buy
guild-exchange-news-any-good-7 = {$company} launches product people actually want
guild-exchange-news-any-good-8 = {$company} settles lawsuit for pocket change
guild-exchange-news-any-good-9 = Regulators close {$company} file
guild-exchange-news-any-good-10 = {$company} supply chain back to full speed
guild-exchange-news-any-good-11 = {$company} announces share buyback
guild-exchange-news-any-good-12 = {$company} wins back customer lost last year
guild-exchange-news-any-good-13 = {$company} cuts costs without cutting output
guild-exchange-news-any-good-14 = Big fund discloses new {$company} stake
guild-exchange-news-any-good-15 = {$company} insiders buy shares on open market
guild-exchange-news-any-good-16 = {$company} credit rating raised a notch
guild-exchange-news-any-good-17 = {$company} lands NanoTrasen partnership
guild-exchange-news-any-good-18 = {$company} takeover bid rumoured at fat premium
guild-exchange-news-any-good-19 = {$company} pays down debt early
guild-exchange-news-any-good-20 = {$company} customer satisfaction jumps after redesign
guild-exchange-news-any-bad-1 = {$company} misses on revenue, trims guidance
guild-exchange-news-any-bad-2 = {$company} CEO resigns, effective immediately
guild-exchange-news-any-bad-3 = {$company} overpaid for acquisition, analysts say
guild-exchange-news-any-bad-4 = {$company} loses major contract
guild-exchange-news-any-bad-5 = Workers strike at {$company} plants
guild-exchange-news-any-bad-6 = Analysts downgrade {$company} to sell
guild-exchange-news-any-bad-7 = {$company} recalls flagship product
guild-exchange-news-any-bad-8 = {$company} hit with large lawsuit
guild-exchange-news-any-bad-9 = Regulators open probe into {$company}
guild-exchange-news-any-bad-10 = Supply problems slow {$company} deliveries
guild-exchange-news-any-bad-11 = {$company} suffers data breach
guild-exchange-news-any-bad-12 = {$company} AI goes 'off-script' in front of customers
guild-exchange-news-any-bad-13 = {$company} sells new shares to raise cash
guild-exchange-news-any-bad-14 = Big fund dumps {$company} stake
guild-exchange-news-any-bad-15 = {$company} insiders sell ahead of quiet period
guild-exchange-news-any-bad-16 = {$company} credit rating cut a notch
guild-exchange-news-any-bad-17 = {$company} partnership with NanoTrasen collapses
guild-exchange-news-any-bad-18 = {$company} restates last quarter's figures
guild-exchange-news-any-bad-19 = {$company} delays product launch indefinitely
guild-exchange-news-any-bad-20 = {$company} cuts jobs as demand cools

## Sector and economy news. These move every company with a stake in the sector.

guild-exchange-news-factor-market-good-1 = Federation central bank cuts rates; markets rally
guild-exchange-news-factor-market-good-2 = Sector trade volumes hit record
guild-exchange-news-factor-market-good-3 = Colonial confidence at 5-year high
guild-exchange-news-factor-market-good-4 = Guild cuts listing fees; money pours in
guild-exchange-news-factor-market-good-5 = Two frontier powers sign peace accord
guild-exchange-news-factor-market-good-6 = Pirate fleet broken up; insurers relax
guild-exchange-news-factor-market-bad-1 = Federation central bank hikes rates in surprise move
guild-exchange-news-factor-market-bad-2 = Frontier bank fails; credit tightens
guild-exchange-news-factor-market-bad-3 = Ion storm closes shipping lanes
guild-exchange-news-factor-market-bad-4 = Colonial confidence slumps after bad census
guild-exchange-news-factor-market-bad-5 = Guild trading systems glitch; panic selling follows
guild-exchange-news-factor-market-bad-6 = Pirates seize convoy; insurers raise rates

guild-exchange-news-factor-plasma-good-1 = Plasma spikes as refinery belt goes dark
guild-exchange-news-factor-plasma-good-2 = Federation fleet orders plasma for long patrol
guild-exchange-news-factor-plasma-good-3 = Cold season lifts plasma heating demand
guild-exchange-news-factor-plasma-good-4 = Frontier frezon demand climbs
guild-exchange-news-factor-plasma-good-5 = Cartel agrees to cut plasma output
guild-exchange-news-factor-plasma-good-6 = Plasma reserve estimate cut; prices jump
guild-exchange-news-factor-plasma-bad-1 = Plasma glut as new fields come online
guild-exchange-news-factor-plasma-bad-2 = Federation caps plasma prices
guild-exchange-news-factor-plasma-bad-3 = Plasma cartel output deal collapses
guild-exchange-news-factor-plasma-bad-4 = Cheap fusion cells eat into plasma demand
guild-exchange-news-factor-plasma-bad-5 = Plasma tanker accident prompts new safety rules
guild-exchange-news-factor-plasma-bad-6 = Warm season cuts plasma heating demand

guild-exchange-news-factor-frontier-good-1 = Frontier colonies boom; supply orders pile up
guild-exchange-news-factor-frontier-good-2 = New jump lane opens to outer colonies
guild-exchange-news-factor-frontier-good-3 = Freight rates fall as new haulers join routes
guild-exchange-news-factor-frontier-good-4 = Settlers flood newly surveyed system
guild-exchange-news-factor-frontier-good-5 = Colonial governments double supply budgets
guild-exchange-news-factor-frontier-good-6 = Asteroid mining rush draws thousands of crews
guild-exchange-news-factor-frontier-bad-1 = Frontier jump lane closed for repairs
guild-exchange-news-factor-frontier-bad-2 = Freight rates soar on hauler strike
guild-exchange-news-factor-frontier-bad-3 = Colonies cut spending as harvests fail
guild-exchange-news-factor-frontier-bad-4 = Outbreak quarantines frontier trade hub
guild-exchange-news-factor-frontier-bad-5 = Ore rush ends; mining crews head home
guild-exchange-news-factor-frontier-bad-6 = Pirates block main frontier supply route

guild-exchange-news-factor-syndicate-good-1 = Federation patrols thin out on frontier
guild-exchange-news-factor-syndicate-good-2 = Sanctions on 'certain organisations' eased
guild-exchange-news-factor-syndicate-good-3 = Black market prices firm, sources say
guild-exchange-news-factor-syndicate-good-4 = Court throws out major smuggling case
guild-exchange-news-factor-syndicate-good-5 = Border systems ignore Federation export rules
guild-exchange-news-factor-syndicate-good-6 = Customs officers strike at three ports
guild-exchange-news-factor-syndicate-bad-1 = Federation cracks down on smuggling networks
guild-exchange-news-factor-syndicate-bad-2 = New sanctions target 'certain organisations'
guild-exchange-news-factor-syndicate-bad-3 = Marshals raid warehouses across frontier
guild-exchange-news-factor-syndicate-bad-4 = Banks told to freeze suspect accounts
guild-exchange-news-factor-syndicate-bad-5 = Informant testimony rattles black market
guild-exchange-news-factor-syndicate-bad-6 = Customs doubles outbound freight inspections

guild-exchange-news-factor-rivalry-good-1 = NanoTrasen wins round in Cybersun patent war
guild-exchange-news-factor-rivalry-good-2 = Federation backs NanoTrasen in Cybersun contract row
guild-exchange-news-factor-rivalry-good-3 = Cybersun engineers defect to NanoTrasen in numbers
guild-exchange-news-factor-rivalry-good-4 = Colonial fleet picks NanoTrasen hardware over Cybersun
guild-exchange-news-factor-rivalry-good-5 = Cybersun loses lawsuit brought by NanoTrasen
guild-exchange-news-factor-rivalry-good-6 = NanoTrasen undercuts Cybersun on borg chassis
guild-exchange-news-factor-rivalry-bad-1 = Cybersun wins round in NanoTrasen patent war
guild-exchange-news-factor-rivalry-bad-2 = Federation backs Cybersun in NanoTrasen contract row
guild-exchange-news-factor-rivalry-bad-3 = NanoTrasen researchers defect to Cybersun in numbers
guild-exchange-news-factor-rivalry-bad-4 = Colonial fleet picks Cybersun hardware over NanoTrasen
guild-exchange-news-factor-rivalry-bad-5 = NanoTrasen loses lawsuit brought by Cybersun
guild-exchange-news-factor-rivalry-bad-6 = Cybersun undercuts NanoTrasen on AI cores

## Early warnings: small stories that may come to something a few minutes later, or to nothing.

guild-exchange-story-call-delay = {$company} quietly delays quarterly call
guild-exchange-story-call-delay-good = {$company} delay tied to major contract signing
guild-exchange-story-call-delay-bad = {$company} admits accounting error behind delay

guild-exchange-story-exec-flight = {$company} executives seen leaving HQ late at night
guild-exchange-story-exec-flight-good = {$company} confirms merger talks
guild-exchange-story-exec-flight-bad = {$company} board ousts CEO

guild-exchange-story-filing = {$company} files unusual notice with Guild
guild-exchange-story-filing-good = {$company} notice reveals large buyback
guild-exchange-story-filing-bad = {$company} notice reveals big write-down

guild-exchange-story-plant-quiet = {$company} plant reported idle
guild-exchange-story-plant-quiet-bad = {$company} confirms production halt
guild-exchange-story-plant-quiet-good = {$company} plant restarts after planned refit

guild-exchange-story-lawyers = {$company} hires expensive law firm
guild-exchange-story-lawyers-bad = Former partner sues {$company}
guild-exchange-story-lawyers-good = {$company} wins patent claim against rival

guild-exchange-story-shipping = Unusual {$company} freighter traffic spotted
guild-exchange-story-shipping-good = {$company} confirms big new customer
guild-exchange-story-shipping-bad = {$company} freighters were moving stock to cover recall

guild-exchange-story-auditor = {$company} changes auditors mid-year
guild-exchange-story-auditor-bad = New auditor flags problems in {$company} books
guild-exchange-story-auditor-good = New auditor gives {$company} clean bill

guild-exchange-story-hiring = {$company} posts hundreds of job listings overnight
guild-exchange-story-hiring-good = {$company} ramps up for large order
guild-exchange-story-hiring-bad = {$company} replacing striking staff, union says

guild-exchange-story-silence = {$company} press office stops answering questions
guild-exchange-story-silence-bad = {$company} confirms data breach
guild-exchange-story-silence-good = {$company} breaks silence with product launch

guild-exchange-story-regulator = Federation regulators request {$company} documents
guild-exchange-story-regulator-good = Regulators clear {$company} of wrongdoing
guild-exchange-story-regulator-bad = {$company} fined heavily after probe

guild-exchange-story-insider = Large {$company} block changes hands off the books
guild-exchange-story-insider-good = Fund reveals it has been building {$company} stake
guild-exchange-story-insider-bad = {$company} founder confirms selling down

guild-exchange-story-meeting = {$company} calls surprise shareholder meeting
guild-exchange-story-meeting-good = {$company} holders approve special payout
guild-exchange-story-meeting-bad = {$company} holders vote down board's plan

## Earnings, dividends and cargo orders.

guild-exchange-earnings-beat = {$company} beats; EPS {$actual} vs {$consensus} est.
guild-exchange-earnings-miss = {$company} misses; EPS {$actual} vs {$consensus} est.
guild-exchange-earnings-inline = {$company} in line; EPS {$actual} vs {$consensus} est.
guild-exchange-dividend = {$company} pays dividend of {$amount} a share

guild-exchange-orders-1 = Strong frontier orders lift {$company}
guild-exchange-orders-2 = Station supply orders keep {$company} busy
guild-exchange-orders-3 = {$company} order book swells on station demand
guild-exchange-orders-4 = Cargo departments buy {$company} goods in bulk
guild-exchange-orders-5 = Run on {$company} catalogue reported
guild-exchange-orders-6 = {$company} can't ship fast enough, supply desks say

## Announced station events.

guild-exchange-shock-meteors-small = Meteor showers reported near NanoTrasen stations
guild-exchange-shock-meteors-medium = Meteor storm batters NanoTrasen stations in sector
guild-exchange-shock-meteors-large = Major meteor storm hits NanoTrasen station
guild-exchange-shock-rod = High-velocity object tears through NanoTrasen station
guild-exchange-shock-gas-leak = Gas leak reported aboard NanoTrasen station
guild-exchange-shock-power = NanoTrasen station grid failure under review
guild-exchange-shock-blackout = Comms blackout hits NanoTrasen station
guild-exchange-shock-solar-flare = Solar flare disrupts electronics across sector
guild-exchange-shock-anomaly = Anomaly at NanoTrasen station draws research interest
guild-exchange-shock-rad-storm = Radiation storm sweeps sector
guild-exchange-shock-creatures = Creature infestation reported aboard NanoTrasen station
guild-exchange-shock-vent-clog = Unknown foam contaminates station ventilation
guild-exchange-shock-paperwork = NanoTrasen station paperwork mix-up goes viral
guild-exchange-shock-ert-security = Emergency security team sent to NanoTrasen station
guild-exchange-shock-ert-medical = Emergency medical team sent to NanoTrasen station
guild-exchange-shock-pizza = Pizza party shipment heads to NanoTrasen station
