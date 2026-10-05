/**
 * Alle teksten van de app, op één plek.
 *
 * Toon: Nederlandse spreektaal, je/jij, kort en praktisch — zoals je het tegen een maat in de
 * auto zou zeggen. Geen ambtelijke taal, geen Engels waar een gewoon Nederlands woord bestaat.
 * Vaste woorden: "rit" (een drive), "rijders" (andere weggebruikers in de app), "kanaal"
 * (de spraakgroep), "in de buurt", "onderweg".
 */
import { formatDistance, formatElapsedMinutes } from '../utils/format';

const rijders = (n: number) => `${n} ${n === 1 ? 'rijder' : 'rijders'}`;

export const nl = {
  anonymousName: 'Rijder',

  tabs: {
    drive: 'Rijden',
    nearby: 'In de buurt',
    voice: 'Praten',
    settings: 'Instellingen',
  },

  welcome: {
    // Bewuste regelafbreking: anders breekt de grote kop af op "Samen op de / weg."
    title: 'Samen\nop de weg.',
    body: 'Rijders bij jou in de buurt hoor je meteen. Handsfree, zonder gedoe. Rij je uit elkaar? Dan ben je vanzelf weer los.',
    getStarted: 'Aan de slag',
    haveAccount: 'Ik heb al een account',
  },

  auth: {
    signInTitle: 'Welkom terug',
    signInBody: 'Log in en rij verder.',
    signUpTitle: 'Maak je account aan',
    signUpBody: 'Kies de naam die rijders in de buurt zien. Liever anoniem? Dat regel je later bij Privacy.',
    name: 'Naam',
    email: 'E-mail',
    password: 'Wachtwoord',
    passwordHint: 'Minimaal 8 tekens.',
    signIn: 'Inloggen',
    signUp: 'Account aanmaken',
    errors: {
      network: 'Geen verbinding met Opdeweg. Check je internet even.',
      invalidCredentials: 'E-mail of wachtwoord klopt niet.',
      emailTaken: 'Er is al een account met dit e-mailadres.',
      rateLimited: 'Even rustig aan, te veel pogingen. Probeer het zo nog een keer.',
      generic: 'Er ging iets mis. Probeer het nog een keer.',
    },
  },

  connection: {
    connected: 'Verbonden',
    connecting: 'Verbinden…',
    offline: 'Offline',
    serverUnreachable: 'Server onbereikbaar',
    a11y: (label: string) => `Verbinding: ${label}`,
  },

  drive: {
    titleIdle: 'Klaar voor vertrek?',
    bodyIdle: 'Start je rit en praat handsfree met rijders om je heen.',
    titleActive: 'Onderweg',
    bodyActive: (minutes: number) => (minutes < 1 ? 'Net vertrokken' : `${formatElapsedMinutes(minutes)} onderweg`),
    start: 'Start',
    stop: 'Stop',
    startA11y: 'Rit starten',
    stopA11y: 'Rit stoppen',
    startHint: 'Deelt je geschatte locatie en zet je vanzelf in een kanaal met rijders in de buurt',
    stopHint: 'Stopt je rit en haalt je uit het spraakkanaal',
    nearbyLabel: 'In de buurt',
    nearbyValue: (n: number) => rijders(n),
    nearbyNone: '—',
    nearbyWithin: 'binnen 1 km',
    nearbySearching: 'we kijken om je heen',
    nearbyStartFirst: 'start eerst je rit',
    voiceLabel: 'Praten',
    voiceInChannel: (n: number) => `${rijders(n)} in je kanaal`,
    voiceAutomatic: 'gaat vanzelf',
    privacyLocation: 'Locatie gedeeld tijdens je rit',
    micLive: 'Mic live',
    micMuted: 'Mic op stil',
    micIdle: 'Mic uit',
    confirmStop: {
      title: 'Rit stoppen?',
      body: 'Je gaat uit het spraakkanaal en je deelt je locatie niet meer.',
      keepDriving: 'Doorrijden',
      stop: 'Stoppen',
    },
    startFailed: {
      locationOffTitle: 'Je locatie staat uit',
      locationOffBody: 'Zet Locatievoorzieningen aan, dan vinden we rijders bij jou in de buurt.',
      offlineTitle: 'Je bent offline',
      offlineBody: 'Maak eerst verbinding met internet, dan kun je je rit starten.',
      errorTitle: 'Rit starten lukt even niet',
      errorBody: 'Probeer het zo nog een keer.',
    },
  },

  voiceState: {
    off: 'Uit',
    standby: 'Stand-by',
    connecting: 'Verbinden',
    live: 'Live',
    reconnecting: 'Opnieuw verbinden',
    retrying: 'Nog een poging',
  },

  controls: {
    micOn: 'Mic aan',
    micMuted: 'Op stil',
    micA11y: 'Microfoon',
    micMuteHint: 'Zet je microfoon op stil',
    micUnmuteHint: 'Zet je microfoon weer aan',
    speaker: 'Luidspreker',
    automatic: 'Automatisch',
    speakerA11y: 'Luidspreker',
    speakerHint: 'Wissel tussen de luidspreker en automatisch (auto-Bluetooth, intercom of oortjes)',
  },

  banners: {
    offlineDriving: 'Geen verbinding. We pakken het vanzelf weer op.',
    offline: 'Je bent offline',
    serverUnreachable: 'Opdeweg is even niet bereikbaar. We blijven het proberen.',
    waitingForGps: 'Wachten op GPS…',
    weakGps: 'Zwak GPS-signaal. Koppelen staat even op pauze.',
    micPermission: 'Microfoon staat uit. Je kunt alleen luisteren.',
    micUnavailable: 'Microfoon niet beschikbaar. Je kunt alleen luisteren.',
    lastDriveIdle: 'Je vorige rit is gestopt, we hadden je een tijd geen signaal.',
    signedOutElsewhere: 'Rit gestopt: je bent op een ander apparaat uitgelogd.',
    locationDenied: 'Je locatietoegang staat uit',
    reconnecting: 'Live-updates opnieuw verbinden…',
    settings: 'Instellingen',
    allow: 'Toestaan',
    fix: 'Regelen',
  },

  nearby: {
    title: 'In de buurt',
    count: (n: number) => `${rijders(n)} in de buurt`,
    notDriving: 'Je rijdt nu niet',
    inChannel: 'In je kanaal',
    alsoNearby: 'Ook in de buurt',
    speaking: 'Praat nu',
    otherChannel: 'In de buurt · ander kanaal',
    emptyIdleTitle: 'Start je rit om te zien wie er rijdt',
    emptyIdleBody: (joinMeters: number) => `Rijders binnen ${formatDistance(joinMeters)} zie je hier vanzelf verschijnen.`,
    emptyDrivingTitle: 'Nog niemand in de buurt',
    emptyDrivingBody: 'Zodra er iemand binnen bereik is, zetten we je erbij. Je hoeft zelf niks te doen.',
    privacyNote: 'Afstanden zijn een schatting. Je exacte locatie delen we nooit met andere rijders.',
    rowA11y: (name: string, distance: string, inChannel: boolean, speaking: boolean) =>
      `${name}, ongeveer ${distance} verderop${inChannel ? ', in je kanaal' : ''}${speaking ? ', praat nu' : ''}`,
  },

  voice: {
    title: 'Spraakkanaal',
    offTitle: 'Praten staat uit',
    offBody: 'Start je rit om met rijders in de buurt te praten',
    speaking: 'Praat nu',
    you: 'Jij',
    quietTitle: 'Stil op de weg',
    listening: (n: number) => `${rijders(n)} ${n === 1 ? 'luistert' : 'luisteren'} mee`,
    standbyTitle: 'Stand-by',
    standbyBody: 'We zetten je erbij zodra er rijders in de buurt zijn',
    connectingTitle: 'Verbinden…',
    connectingBody: 'Je komt zo in het spraakkanaal',
    inChannelA11y: (n: number) => `${rijders(n)} in je kanaal`,
    stopDrive: 'Rit stoppen',
  },

  avatar: {
    speakingA11y: (name: string) => `${name}, praat nu`,
  },

  permissions: {
    title: 'Nog even instellen',
    body: 'Drie snelle toestemmingen en je kunt handsfree de weg op.',
    optional: 'optioneel',
    locationTitle: 'Locatie tijdens je rit',
    locationBody: 'Zo vinden we rijders binnen 1 km. Alleen tijdens je rit, en anderen zien nooit waar je bent: alleen een afgeronde afstand.',
    micTitle: 'Microfoon',
    micBody: "Alleen live als er rijders in de buurt zijn. Met één tik zet je 'm op stil.",
    backgroundTitleIos: 'Ook met je scherm uit',
    backgroundTitleAndroid: 'Altijd toestaan',
    backgroundBody: 'Aanrader: zo blijf je verbonden met je scherm uit, ook als je telefoon de app even herstart. We gebruiken je locatie echt alleen tijdens je rit.',
    done: 'Klaar',
    openSettings: 'Open Instellingen',
    allowLocation: 'Locatie toestaan',
    allowMicrophone: 'Microfoon toestaan',
    allowBackground: 'Altijd toestaan',
    notNow: 'Niet nu',
    later: 'Later',
  },

  settings: {
    title: 'Instellingen',
    account: 'Account',
    displayName: 'Naam',
    signOut: 'Uitloggen',
    privacy: 'Privacy',
    shareName: 'Laat mijn naam zien aan rijders in de buurt',
    shareNameHint: 'Staat dit uit? Dan zien anderen alleen “Rijder”.',
    locationUse: 'Wat we met je locatie doen',
    locationUseBody: 'Alleen tijdens je rit gedeeld, en nooit als exacte locatie. Anderen zien alleen een afgeronde afstand. Je ritten slaan we niet op.',
    permissions: 'Toestemmingen',
    location: 'Locatie',
    backgroundLocation: 'Locatie op de achtergrond',
    microphone: 'Microfoon',
    bluetooth: 'Apparaten in de buurt (Bluetooth)',
    permissionGranted: 'Toegestaan',
    permissionDenied: 'Geweigerd',
    permissionNotNeeded: 'Niet nodig',
    permissionNotSet: 'Nog niet ingesteld',
    audio: 'Geluid',
    useSpeaker: 'Luidspreker gebruiken',
    useSpeakerHint: 'Uit: we kiezen zelf je auto-Bluetooth, intercom of oortjes.',
    chooseAudioDevice: 'Audioapparaat kiezen',
    voice: 'Praten',
    startMuted: 'Rit starten met mic op stil',
    keepAwake: 'Scherm aan tijdens je rit',
    locationAndRange: 'Locatie & bereik',
    range: 'Bereik',
    rangeBody: (join: number, leave: number) =>
      `Binnen ${formatDistance(join)} kom je in het kanaal, verder dan ${formatDistance(leave)} ga je eruit. Dat verschil voorkomt geflikker aan de rand.`,
    updateFrequency: 'Locatie-updates',
    updateFrequencyBody: (meters: number, seconds: number) => `Na elke ${meters} m of elke ${seconds} seconden. Nooit continu.`,
    appearance: 'Weergave',
    system: 'Systeem',
    dark: 'Donker',
    light: 'Licht',
    about: 'Over',
    deleteAccount: 'Account verwijderen',
    confirmSignOut: { title: 'Uitloggen?', body: 'Je rit stopt dan ook.', cancel: 'Annuleren', confirm: 'Uitloggen' },
    confirmDelete: {
      title: 'Account verwijderen?',
      body: 'Hiermee verwijder je je account en al je gegevens. Dit kun je niet terugdraaien.',
      cancel: 'Annuleren',
      confirm: 'Verwijderen',
    },
    updateFailed: { title: 'Aanpassen lukt even niet', body: 'Probeer het nog een keer als je online bent.' },
    deleteFailed: { title: 'Verwijderen lukt even niet', body: 'Probeer het nog een keer als je online bent.' },
  },

  editName: {
    title: 'Je naam',
    body: 'Dit zien rijders in de buurt, tenzij je het uitzet bij Privacy.',
    label: 'Naam',
    save: 'Opslaan',
    cancel: 'Annuleren',
    failed: 'Opslaan lukt niet. Probeer het nog een keer.',
  },

  network: {
    noConnection: 'Geen verbinding met Opdeweg.',
    signInAgain: 'Log even opnieuw in.',
    requestFailed: (status: number) => `Er ging iets mis (${status}).`,
  },

  notification: {
    title: 'Opdeweg · Onderweg',
    body: 'Je deelt je geschatte locatie met rijders in de buurt. Praten gaat vanzelf zodra er iemand dichtbij is.',
    locationOnlyBody: 'Je deelt je geschatte locatie met rijders in de buurt.',
  },
} as const;

export const t = nl;
