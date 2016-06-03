<?php
/****************************************************************************
 *
 * @package TrumaConduit
 * 
 * Trumba calendar interface
 *
 * Use Trumba's API to submit single .ics events to a given calendar.
 * The events to be written are represented by simple PHP hashes, where
 * the key/value pairs represent the fields and values of the event.
 * The following properties are required for each
 * submitted event:
 *
 * UID      A guaranteed globally unique ID for the event
 * DTSTART  Start time
 * SUMMARY  Description of the event
 *  
 * Internally, this hash is converted to a VEVENT which composes the
 * icalbody of a calendaring core object (VCALENDAR).  This VCALENDAR
 * is submitted to the Trumba server by doing a PUT to a specially-crafted
 * URL that identifies the desired calendar.
 *
 * Usage:
 *
 *   $config = array('prodid'    => '//your company//your app/Language',
 *                   'useragent' => 'Your Company Calendar Automation');
 *   $trumba = new TrumbaConduit('your_cal', 'your_email',
 *                               'your_password', $config);
 *   
 * Author: Dan Boren, 
 *         Computer Science & Engineering University of Washington
 **************************************************************************/

class TrumbaConduit {
  
  // Local config
  private $timezone  = 'America/Los_Angeles';  
  private $version   = '2.0';
  private $prodid    = '//UW//Calendar//English';
  private $useragent = 'Trumba Calendar Automation';
  // End local config
  
  private $url_base = 'https://www.trumba.com/service';
  
  private $username  = null;
  private $password = null;
  private $cal_name = null;
  private $url      = null;
  
  private $required_fields = array('UID', 'DTSTART', 'SUMMARY');
  private $tz_fields = array('DTSTART', 'DTEND');
  
  private $last_response = array(); 
  // Response from last PUT.  Client access is via get_last_action_info()
  
  /*****
   * Build an object used to submit events to trumba calendar.  A username/password
   * is supplied to authenticate using basic auth.
   * 
   * @param string $cal_name     Name of the calendar you're updating
   * @param string $username     Usually, an email address having author rights to calendar 
   * @param string $password     Usually, a trumba-supplied password, not your kerberos password
   * @param array  $local_config Hash of values to override defaults.  Currently supported keys:
   *                             timezone  - standard timezone identifier
   *                             prodid    - used in PRODID field of vcalendar
   *                             useragent - a courtesy string identifying your activities in
   *                                         trumba's server logs
   *
   */
  function __construct($cal_name, $username = null, $password = null, $local_config = array()) {
    $this->cal_name = $cal_name;
    $this->username = $username;
    $this->password = $password;
    
    // NB: since we only support PUTting a single event, delta is hard-coded
    //     to true.
    $this->url = "{$this->url_base}/{$cal_name}.ics?delta=true";
    
    // Override any local config
    foreach ($local_config as $varname => $value) {
      ${$varname} = $value;
    }
  }
  

  /*****
   * Send a single vevent to the trumba calendar
   *
   * The supplied data are turned into a VCALENDAR object and
   * sent to the trumba server.
   * 
   * @param array $data    Hash containing ics fields (key => value)
   * @param string $method PUBLISH by default, 
   *                       but change to other standard methods
   *                       (e.g. CANCEL) as needed
   *                       
   * @see http://www.trumba.com/help/api/icsimport.aspx
   *
   * @return bool True on success.  If false, get further info with
   *         get_last_action_info()
   */
  function put_event($data, $method = 'PUBLISH') {
    
    // Build the ics content for the supplied data.
    $payload  = "BEGIN:VCALENDAR\n";
    $payload .= "METHOD:{$method}\n";
    $payload .= "VERSION:{$this->version}\n";
    $payload .= "PRODID:-//UW Computer Science//Colloquia//English\n";
    $payload .= $this->make_vevent($data);
    $payload .= "END:VCALENDAR\n";
    
    // cUrl wants our data in a file; make a temporary one in memory
    $fp = fopen('php://temp/maxmemory:128000', 'w');
    if (!$fp) {
      throw new Exception('put_event(): unable to open temporary file');
    }
    fwrite($fp, $payload);
    fseek($fp, 0);
        
    // Set up the http PUT
    $ch = curl_init($this->url);
    curl_setopt($ch, CURLOPT_USERAGENT, $this->useragent);
    curl_setopt($ch, CURLOPT_PUT, true);
    
    // basic auth
    curl_setopt($ch, CURLOPT_HTTPAUTH, CURLAUTH_BASIC);
    curl_setopt($ch, CURLOPT_USERPWD, $this->username . ":" . $this->password);
    
    
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Content-Type: text/calendar',
                                               'Content-Length: ' . strlen($payload)
                                               )
                );    
    curl_setopt($ch, CURLOPT_INFILE, $fp);
    // This also works in place of the CURLOPT_PUT and CURLOPT_INFILE statements
    // above:
    //curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "PUT"); 
    //curl_setopt($ch, CURLOPT_POSTFIELDS,$payload);
    
    // I don't know why, but this doesn't work.  Setting it in CURLOPT_HTTPHEADER
    // does, though.
    //curl_setopt($ch, CURLOPT_INFILESIZE, strlen($vevent));

    // PUT the event, collect the results
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    $result = curl_exec($ch);
    $return_code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    
    $this->parse_response($result);
    return(($return_code >= 200) and ($return_code < 300));
  }
  
  /*****
   * Retrieve the result of and information about the last operation
   */
  function get_last_action_info() {
    if (empty($this->last_response))
      throw new Exception('No response available because no action has yet been taken');
    
    return($this->last_response);
  }
  
  /*****
   * Return a VEVENT representation of the supplied data.
   * 
   * @param array $data Hash containing ics fields (key => value)
   *
   * @return string VEVENT from supplied data
   */
  function make_vevent($data) {
    foreach($this->required_fields as $rf) {
      if (! isset($data[$rf]) or empty($data[$rf])) {
        throw new Exception("Required field $rf not found");
      }
    }
    
    $vevent = "BEGIN:VEVENT\n";
    
    foreach ($data as $field => $val) {
      if (in_array($field, $this->tz_fields)) {
        $field .= ";TZID={$this->timezone}";
      }
      
      $vevent .= "{$field}:{$val}\n";
    }
    
    $vevent .= "END:VEVENT\n";
    
    return($vevent);
  }
  

  // Escapes a string of characters
  function escape_string($string) {
    return preg_replace('/([\,;])/','\\\$1', $string);
  }

  /*****
   * The response of the PUT operation is represented by an XML object.  Parse
   * it into an internal hash for ease of use.
   * 
   * @param string $xml_response XML return from a PUT operation
   */
  private function parse_response($xml_response) {
    $parser = xml_parser_create();
    xml_parse_into_struct($parser, $xml_response, $vals, $index);
    xml_parser_free($parser);
    
    $response = $vals[$index['RESPONSEMESSAGE'][0]];
    $this->last_response['code'] = $response['attributes']['CODE'];
    $this->last_response['description'] = $response['attributes']['DESCRIPTION'];
    $this->last_response['level'] = $response['attributes']['LEVEL'];
  }
  
}
?>
